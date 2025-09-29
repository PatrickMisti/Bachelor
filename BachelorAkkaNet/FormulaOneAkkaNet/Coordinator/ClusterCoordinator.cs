using Akka.Actor;
using Akka.Event;
using Akka.Hosting;
using FormulaOneAkkaNet.Coordinator.Broadcasts;
using FormulaOneAkkaNet.Coordinator.Listeners;
using FormulaOneAkkaNet.Coordinator.Messages;
using Infrastructure.PubSub.Messages;

namespace FormulaOneAkkaNet.Coordinator;

public class ClusterCoordinator : ReceiveActor
{
    private readonly ILoggingAdapter _logger = Context.GetLogger();

    private readonly IActorRef _shardListener;
    private readonly IActorRef _ingressListener;

    // Shard region connection status
    private bool _hasShardRegion;

    public ClusterCoordinator(IRequiredActor<ShardListener> shardListener, IRequiredActor<IngressListener> ingressListener)
    {
        _logger.Info("ClusterController started");

        _shardListener = shardListener.ActorRef;
        _ingressListener = ingressListener.ActorRef;

        HandleShardListener();
    }

    private void HandleShardListener()
    {
        Receive<ShardConnectionUpdateMessage>(msg =>
        {
            _hasShardRegion = msg.IsShardOnline;
            var con = _hasShardRegion ? "connected" : "disconnected";
            _logger.Debug($"Shard connection changed. Shard {con}");
            _ingressListener.Tell(new IngressConnectionCanActivated(_hasShardRegion));
        });

        Receive<IngressConnectionUpdateMessage>(_ =>
        {
            _logger.Debug("Ingress is started and should be notified to act/deact of shard status");
            _ingressListener.Tell(new IngressConnectionCanActivated(_hasShardRegion));
        });

        ReceiveAsync<IngressConnectivityRequest>(async _ =>
        {
            if (!_hasShardRegion)
            {
                var res = await _shardListener.Ask<ShardConnectionUpdateMessage>(ShardConnectionRequest.Instance);
                _hasShardRegion = res.IsShardOnline;
            }

            _logger.Debug($"Ingress activate request. Shard active: {_hasShardRegion}");
            Sender.Tell(new IngressConnectivityResponse(_hasShardRegion));
        });

        ReceiveAsync<NodeInClusterRequest>(async _ =>
        {
            var shardCount = await _shardListener.Ask<ShardCountResponse>(ShardCountRequest.Instance);
            var ingressCount = await _ingressListener.Ask<IngressCountResponse>(IngressCountRequest.Instance);
            _logger.Info($"Status: Shard count: {shardCount.ActiveShards}, Ingress count: {ingressCount.ActiveIngress}");

            var result = shardCount.ActiveShards + ingressCount.ActiveIngress;
            Sender.Tell(new NodeInClusterResponse(result));
        });
    }
}