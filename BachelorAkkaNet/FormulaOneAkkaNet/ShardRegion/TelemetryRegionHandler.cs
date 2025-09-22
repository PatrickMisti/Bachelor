using Akka.Actor;
using Akka.Cluster.Sharding;
using Akka.Event;
using Akka.Hosting;
using FormulaOneAkkaNet.ShardRegion.Messages;
using FormulaOneAkkaNet.ShardRegion.Utilities;
using Infrastructure;
using Infrastructure.PubSub;
using Infrastructure.ShardRegion;
using Infrastructure.ShardRegion.Messages;

namespace FormulaOneAkkaNet.ShardRegion;

public sealed class TelemetryRegionHandler(IRequiredActor<DriverRegionMarker> shardRegion)
    : ReceivePubSubActor<IPubSubTopicBackend>
{
    private readonly ILoggingAdapter _logger = Context.GetLogger();

    private readonly TimeSpan _timeout = TimeSpan.FromSeconds(5);

    public override void Activated()
    {
        _logger.Info("Subscription is up");
        ReceiveAsync<GetDriverStateRequest>(DriverStateHandler);
        Receive<UpdatedDriverMessage>(NotifyUpdatedDriver);
    }

    private async Task DriverStateHandler(GetDriverStateRequest msg)
    {
        if (msg.Key is null)
        {
            Sender.Tell(new GetDriverStateResponse(msg.Key, $"{typeof(GetDriverStateRequest)} id was empty"));
            return;
        }

        try
        {
            if (!await IsDriverIdInRegion(msg.Key))
            {
                Sender.Tell(new GetDriverStateResponse(msg.Key, $"{msg.Key} not found in region"));
                return;
            }

            var res = await shardRegion.ActorRef
                .Ask<DriverStateMessage>(new GetDriverStateMessage(msg.Key), _timeout);

            _logger.Info($"Sender driver state to {res.State} back");
            Sender.Tell(new GetDriverStateResponse(msg.Key, res.State!));
        }
        catch (DriverInShardNotFoundException e)
        {
            _logger.Warning("Driver not found: {Id}", e.Id);
            Sender.Tell(new GetDriverStateResponse(e.Id, e.Message));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error while handling GetDriverStateRequest for {Id}", msg.Key);
            Sender.Tell(new GetDriverStateResponse(msg.Key, ex.Message));
        }
    }

    private void NotifyUpdatedDriver(UpdatedDriverMessage msg)
    {
        _logger.Info($"Send updated state to api {msg.Key.DriverNumber}");
        Context.PubSub().Api.Publish(new NotifyDriverStateMessage(msg.Key, msg.State));
    }

    private async Task<bool> IsDriverIdInRegion(DriverKey id)
    {
        var stats = await shardRegion.ActorRef
            .Ask<CurrentShardRegionState>(GetShardRegionState.Instance, _timeout);

        return stats.Shards.Any(s => s.EntityIds.Contains(id.ToString()));
    }
}