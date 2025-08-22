using Akka.Actor;
using Akka.Event;
using Infrastructure.Models;
using Infrastructure.Shard.Exceptions;
using Infrastructure.Shard.Messages;
using Infrastructure.Shard.Responses;

namespace DiverShardHost.Actors;

public class DriverActor : ReceiveActor
{
    //private readonly ILoggingAdapter _logger = Logging.GetLogger(Context.System, nameof(DriverActor));
    private readonly ILoggingAdapter _logger = Context.GetLogger();
    private readonly DriverState _state;
    private readonly string _entityId;


    public DriverActor()
    {
        _logger.Info("DriverActor constructor");
        _entityId = Self.Path.Name;
        _state = new();

        Receive<UpdateDriverTelemetry>(HandleUpdateDriverTelemetry);
        Receive<GetDriverState>(HandleGetDriverState);
    }

    private void HandleUpdateDriverTelemetry(UpdateDriverTelemetry message)
    {
        string mid = message.DriverId;

        // Guard: Safety–Net, that the message has a valid DriverId
        if (!string.IsNullOrEmpty(mid) && mid != _entityId)
        {
            _logger.Warning("Ignoring telemetry for mismatched DriverId. Entity={EntityId}, Msg={MsgId}", _entityId, mid);
            Sender.Tell(new Status.Failure(new DriverInShardNotFoundException($"DriverId mismatch. Entity={_entityId}, Msg={mid}")));
            return;
        }

        _state.Apply(message);
        _logger.Debug("Telemetry update for DriverId={DriverId} Lap={Lap} Pos={Pos}", _entityId, _state.LapNumber, _state.PositionOnTrack);

        Sender.Tell(Status.Success.Instance);
    }

    private void HandleGetDriverState(GetDriverState message)
    {
        string mid = message.DriverId;

        if (!string.IsNullOrEmpty(mid) && mid != _entityId)
        {
            Sender.Tell(DriverStateResponse.Failure(new DriverInShardNotFoundException(mid)));
            return;
        }

        _logger.Debug("GetDriverState for DriverId={DriverId}", _entityId);
        Sender.Tell(new DriverStateResponse(_entityId, _state));
    }

    protected override void PreStart() => _logger.Info("DriverActor({EntityId}) started", Self.Path.Name);
    protected override void PostStop() => _logger.Info("DriverActor({EntityId}) stopped", Self.Path.Name);
}