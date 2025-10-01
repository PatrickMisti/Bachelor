using Akka.Actor;
using Akka.Cluster.Sharding;
using Akka.Event;
using Akka.Hosting;
using Akka.Persistence;
using FormulaOneAkkaNet.ShardRegion.Messages;
using FormulaOneAkkaNet.ShardRegion.Utilities;
using Infrastructure.PubSub;
using Infrastructure.ShardRegion;
using Infrastructure.ShardRegion.Messages;

namespace FormulaOneAkkaNet.ShardRegion;

public class DriverActorPersistent : ReceivePersistentActor
{
    public override string PersistenceId => $"driver-{Self.Path.Name}";

    private readonly ILoggingAdapter _logger = Context.GetLogger();
    private readonly DriverInfoState _state = new();
    private readonly IActorRef _handler;

    public DriverActorPersistent(IRequiredActor<TelemetryRegionHandler> handler)
    {
        _handler = handler.ActorRef;
        _logger.Info($"DriverActorPersistent constructor: {Self.Path.Name}");
        RecoverState();

        Become(Uninitialized);
    }
    protected override void PreStart() => _logger.Debug("DriverActor({EntityId}) started", Self.Path.Name);
    protected override void PostStop() => _logger.Debug("DriverActor({EntityId}) stopped", Self.Path.Name);

    private void Uninitialized()
    {
        Command<CreateModelDriverMessage>(m =>
        {
            Persist(m, evt =>
            {

                try
                {
                    _state.Apply(evt);
                    _logger.Info($"Initialized driver {_state.ToDriverInfoString()})");
                    Sender.Tell(CreatedDriverMessage.Success(_state.Key));
                    // Optional: Idle-Passivation
                    //Context.SetReceiveTimeout(TimeSpan.FromMinutes(2));
                    // Become delete before receives
                    Become(Initialized);
                }
                catch (ArgumentNullException ex)
                {
                    _logger.Error(ex, "Failed to initialize driver with message: {Message}", m);
                    Sender.Tell(new Status.Failure(ex));
                    Context.System.PubSub().Api.Publish(new NotifyStatusFailureMessage(ex.Message));
                }
            });
        });

        Command<StopEntity>(_ => Context.Stop(Self));

        // Other Message: denied + passivate
        CommandAny(msg =>
        {
            var entityId = Self.Path.Name; // Fallback falls Key noch leer
            _logger.Warning($"Received {msg.GetType().Name} before initialization for entity {entityId}. Passivating.");

            Sender.Tell(new NotInitializedMessage(entityId));
            Context.Parent.Tell(new Passivate(new StopEntity()));
            Context.System.PubSub().Api.Publish(new NotifyStatusFailureMessage("DriverActor was not init"));
        });

        
    }

    private void RecoverState()
    {
        Recover<SnapshotOffer>(offer =>
        {
            _logger.Info($"SnapshotOffer for {offer.Snapshot}");
            if (offer.Snapshot is DriverInfoState state)
            {
                _state.RestoreFromSnapshot(state);
                _logger.Info($"Recovered snapshot for driver {_state.ToDriverInfoString()}");
            }
        });

        Recover<IHasDriverId>(evt =>
        {
            _state.Apply(evt);
            _logger.Info("Replayed {Event} for {Pid}", evt.GetType().Name, PersistenceId);
        });

        Recover<RecoveryCompleted>(_ =>
        {
            if (_state.IsInitialized) Become(Initialized);
        });
    }

    private void Initialized()
    {
        Command<UpdateTelemetryMessage>(m =>
        {
            _logger.Debug("Telemetry: {Id} speed={Speed} t={Ts:o}", _state.Key, m.Speed, m.TimestampUtc);
            PersistAndApply(m);
        });

        Command<UpdatePositionMessage>(m =>
        {
            _logger.Debug("Position: {Id} pos={Pos} t={Ts:o}", _state.Key, m.PositionOnTrack, m.TimestampUtc);
            PersistAndApply(m);
        });

        Command<UpdateIntervalMessage>(m =>
        {
            _logger.Debug("Interval: {Id} Δ={Gap}s t={Ts:o}", _state.Key, m.GapToLeaderSeconds, m.TimestampUtc);
            PersistAndApply(m);
        });

        Command<RecordLapMessage>(m =>
        {
            var prev = _state.LapNumber;
            _logger.Info($"Lap {m.LapNumber} for {m.Key} (prev={prev}) L={m.LapTime} S1={m.Sector1} S2={m.Sector2} S3={m.Sector3}");
            PersistAndApply(m);
        });

        Command<UpdateStintMessage>(m =>
        {
            _logger.Info($"Stint {_state.Key}: {m.Compound} start={m.LapStart} end={m.LapEnd} age@start={m.TyreAgeAtStart}");
            PersistAndApply(m);
        });

        Command<RecordPitStopMessage>(m =>
        {
            _logger.Info($"Pit {_state.Key}: lap={m.LapNumber} duration={m.PitDuration}");
            PersistAndApply(m);
        });

        Command<GetDriverStateMessage>(m =>
            Sender.Tell(
                new DriverStateMessage(_state.Key, DriverStateDto.Create(_state))));

        // Idle-Passivation
        /*Receive<ReceiveTimeout>(_ =>
        {
            _logger.Info("Idle timeout for {Id}. Passivating.", _state.Key);
            Context.Parent.Tell(new Passivate(new StopEntity()));
        });*/

        Command<SaveSnapshotSuccess>(_ => _logger.Debug("Snapshot saved at seqNr {0}", _.Metadata.SequenceNr));

        Command<SaveSnapshotFailure>(_ =>
        {
            _logger.Warning("Snapshot failed at seqNr {0}", _.Metadata.SequenceNr);
            Context.System.PubSub().Api.Publish(new NotifyStatusFailureMessage($"Snapshot failed at seqNr {_.Metadata.SequenceNr}"));
        });

        Command<StopEntity>(_ => Context.Stop(Self));
    }

    private void PersistAndApply(IHasDriverId element) 
    {
        if (!_state.IsInitialized || !KeysMatchOrFail(element.Key))
        {
            Sender.Tell(
                new Status.Failure(
                    new DriverInShardNotFoundException(element.Key, $"Key is not {_state.Key} or initialized")));

            Context.System.PubSub().Api.Publish(new NotifyStatusFailureMessage($"Key is not {_state.Key} or initialized"));
            return;
        }

        Persist(element, ev =>
        {
            _state.Apply(ev);
            SendToHandler();
            CreateSnapshot();
        });
    }

    private bool KeysMatchOrFail(DriverKey key)
    {
        if (KeyEquals(key)) return true;

        Sender.Tell(
            new Status.Failure(
                new DriverInShardNotFoundException(key, $"Key is not {_state.Key}")));
        Context.System.PubSub().Api.Publish(new NotifyStatusFailureMessage($"Key is not {_state.Key}"));
        return false;
    }

    private bool KeyEquals(DriverKey other) =>
        _state.Key.SessionId == other.SessionId &&
        _state.Key.DriverNumber == other.DriverNumber;

    private void CreateSnapshot()
    {
        if (LastSequenceNr % 10 == 0)
        {
            _logger.Debug("Creating snapshot for {Pid} at seqNr {Seq}", PersistenceId, LastSequenceNr);
            SaveSnapshot(_state.CopyState());
        }
            
    }

    private void SendToHandler() => 
        _handler.Tell(new UpdatedDriverMessage(_state.Key!, DriverStateDto.Create(_state)));
}