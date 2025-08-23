using Akka.Actor;
using Akka.Cluster.Sharding;
using Akka.Event;
using Akka.Hosting;
using Infrastructure.Cluster.Messages.Notification;
using Infrastructure.Cluster.Messages.RequestMessages;
using Infrastructure.Cluster.Messages.ResponseMessage;
using Infrastructure.General.PubSub;
using Infrastructure.Shard;
using Infrastructure.Shard.Exceptions;
using Infrastructure.Shard.Responses;
using Infrastructure.Shard.Messages;

namespace DiverShardHost.Actors;

public sealed class NotifyDriverStateHandler : ReceivePubSubActor<IPubSubTopicBackend>
{
    private readonly ILoggingAdapter _log = Context.GetLogger();
    private readonly IRequiredActor<DriverRegionMarker> _region;

    private readonly TimeSpan _timeout = TimeSpan.FromSeconds(5);

    public NotifyDriverStateHandler(IRequiredActor<DriverRegionMarker> shardRegion)
    {
        _region = shardRegion;
    }

    public override void Activated()
    {
        _log.Info("Subscription is up");
        ReceiveAsync<GetDriverStateRequest>(DriverStateHandler);

        Receive<UpdatedDriverMessage>(NotifyUpdatedDriver);
    }

    private async Task DriverStateHandler(GetDriverStateRequest msg)
    { 
        _log.Warning("Hallo from PubSub Suksma");
        if (msg is { Id: null } or { Id: "" })
        {
            Sender.Tell(new GetDriverStateResponse(msg.Id ?? "", $"{typeof(GetDriverStateRequest)} id was empty"));
            return;
        }

        try
        {
            var stats = await _region.ActorRef
                .Ask<CurrentShardRegionState>(GetShardRegionState.Instance, _timeout);

            var exists = stats.Shards.Any(s => s.EntityIds.Contains(msg.Id));

            if (!exists)
            {
                Sender.Tell(new GetDriverStateResponse(msg.Id, $"{msg.Id} not found in region"));
                return;
            }

            var res = await _region.ActorRef
                .Ask<DriverStateResponse>(new GetDriverState(msg.Id), _timeout);

            _log.Info($"Sender driver state to {res.State} back");
            Sender.Tell(new GetDriverStateResponse(msg.Id, res.State!));
        }
        catch (DriverInShardNotFoundException e)
        {
            _log.Warning("Driver not found: {Id}", e.Id);
            Sender.Tell(new GetDriverStateResponse(e.Id, e.Message));
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error while handling GetDriverStateRequest for {Id}", msg.Id);
            Sender.Tell(new GetDriverStateResponse(msg.Id, ex.Message));
        }
    }

    private void NotifyUpdatedDriver(UpdatedDriverMessage msg)
    {
        Context.PubSub().Api.Publish(new NotifyDriverStateMessage(msg.DriverId, msg.State));
    }
}