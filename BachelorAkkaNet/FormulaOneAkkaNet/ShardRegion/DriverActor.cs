using Akka.Actor;
using Akka.Event;
using Akka.Hosting;
using Infrastructure.ShardRegion;
using Akka.Cluster.Sharding;
using FormulaOneAkkaNet.ShardRegion.Messages;
using FormulaOneAkkaNet.ShardRegion.Utilities;
using Infrastructure.PubSub;
using Infrastructure.ShardRegion.Messages;

namespace FormulaOneAkkaNet.ShardRegion;

public class DriverActor : ReceiveActor
{
    //private readonly ILoggingAdapter _logger = Logging.GetLogger(Context.System, nameof(DriverActor));
    private readonly ILoggingAdapter _logger = Context.GetLogger();
    private readonly DriverInfoState _state = new();

    private readonly IActorRef _handler;


    public DriverActor(IRequiredActor<TelemetryRegionHandler> handler)
    {
        _handler = handler.ActorRef;
        _logger.Info($"DriverActor constructor: {Self.Path.Name}");

        Become(Uninitialized);
    }

    protected override void PreStart() => _logger.Debug("DriverActor({EntityId}) started", Self.Path.Name);
    protected override void PostStop() => _logger.Debug("DriverActor({EntityId}) stopped", Self.Path.Name);


    private void Uninitialized()
    {
        Receive<CreateModelDriverMessage>(m =>
        {
            _state.Apply(m);
            _logger.Info($"Initialized driver {_state.ToDriverInfoString()})");

            Sender.Tell(CreatedDriverMessage.Success(_state.Key));

            // Optional: Idle-Passivation
            //Context.SetReceiveTimeout(TimeSpan.FromMinutes(2));

            // Become delete before receives
            Become(Initialized);
        });

        Receive<StopEntity>(_ => Context.Stop(Self));

        // Other Message: denied + passivate
        ReceiveAny(msg =>
        {
            var entityId = Self.Path.Name; // Fallback falls Key noch leer
            _logger.Warning($"Received {msg.GetType().Name} before initialization for entity {entityId}. Passivating.");

            Sender.Tell(new NotInitializedMessage(entityId));
            Context.Parent.Tell(new Passivate(new StopEntity()));
            Context.System.PubSub().Api.Publish(new NotifyStatusFailureMessage("DriverActor was not init"));
        });
    }

    private void Initialized()
    {
        Receive<UpdateTelemetryMessage>(m =>
        {
            if (!KeysMatchOrFail(m.Key)) return;
            _state.Apply(m);
            _logger.Debug("Telemetry: {Id} speed={Speed} t={Ts:o}", _state.Key, m.Speed, m.TimestampUtc);
            SendToHandler();
        });

        Receive<UpdatePositionMessage>(m =>
        {
            if (!KeysMatchOrFail(m.Key)) return;
            _state.Apply(m);
            _logger.Debug("Position: {Id} pos={Pos} t={Ts:o}", _state.Key, m.PositionOnTrack, m.TimestampUtc);
            SendToHandler();
        });

        Receive<UpdateIntervalMessage>(m =>
        {
            if (!KeysMatchOrFail(m.Key)) return;
            _state.Apply(m);
            _logger.Debug("Interval: {Id} Δ={Gap}s t={Ts:o}", _state.Key, m.GapToLeaderSeconds, m.TimestampUtc);
            SendToHandler();
        });

        Receive<RecordLapMessage>(m =>
        {
            if (!KeysMatchOrFail(m.Key)) return;
            var prev = _state.LapNumber;
            _state.Apply(m);
            _logger.Info($"Lap {m.LapNumber} for {m.Key} (prev={prev}) L={m.LapTime} S1={m.Sector1} S2={m.Sector2} S3={m.Sector3}");
            SendToHandler();
        });

        Receive<UpdateStintMessage>(m =>
        {
            if (!KeysMatchOrFail(m.Key)) return;
            _state.Apply(m);
            _logger.Info($"Stint {_state.Key}: {m.Compound} start={m.LapStart} end={m.LapEnd} age@start={m.TyreAgeAtStart}");
            SendToHandler();
        });

        Receive<RecordPitStopMessage>(m =>
        {
            if (!KeysMatchOrFail(m.Key)) return;
            _state.Apply(m);
            _logger.Info($"Pit {_state.Key}: lap={m.LapNumber} duration={m.PitDuration}");
            SendToHandler();
        });

        Receive<GetDriverStateMessage>(m =>
        {
            Sender.Tell(new DriverStateMessage(_state.Key, DriverStateDto.Create(_state)));
        });

        // Idle-Passivation
        /*Receive<ReceiveTimeout>(_ =>
        {
            _logger.Info("Idle timeout for {Id}. Passivating.", _state.Key);
            Context.Parent.Tell(new Passivate(new StopEntity()));
        });*/

        Receive<StopEntity>(_ => Context.Stop(Self));
    }

    private bool KeysMatchOrFail(DriverKey key)
    {
        if (KeyEquals(key)) return true;

        // Send response if the key does not match
        Sender.Tell(new Status.Failure(
            new DriverInShardNotFoundException(key, $"Key is not {_state.Key}")
        ));

        Context.System.PubSub().Api.Publish(new NotifyStatusFailureMessage($"Key is not {_state.Key}"));
        return false;
    }

    private bool KeyEquals(DriverKey other) =>
        _state.Key.SessionId == other.SessionId &&
        _state.Key.DriverNumber == other.DriverNumber;

    private void SendToHandler()
    {
        _handler.Tell(new UpdatedDriverMessage(_state.Key!, DriverStateDto.Create(_state)));
    }
}
