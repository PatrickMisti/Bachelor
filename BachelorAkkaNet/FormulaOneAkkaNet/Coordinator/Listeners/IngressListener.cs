using Akka.Actor;
using Akka.Event;
using FormulaOneAkkaNet.Coordinator.Broadcasts;
using FormulaOneAkkaNet.Coordinator.Messages;
using Infrastructure.PubSub;
using Infrastructure.PubSub.Messages;

namespace FormulaOneAkkaNet.Coordinator.Listeners;

public class IngressListener(IActorRef controller) : BaseDebounceListener(controller)
{
    private readonly HashSet<Address> _activeIngress = new();

    public override void Activated()
    {
        Logger.Info("Activate Handlers in ShardListener!");
        HandleClusterEvents();
        HandleEvents();
    }

    private void HandleEvents()
    {
        // External request from ingress to check if shard is available
        // Request from ingress if shard is available
        ReceiveAsync<IngressConnectivityRequest>(async _ =>
        {
            var res = await Controller.Ask<IngressActivateResponse>(IngressActivateRequest.Instance);
            var isActive = _activeIngress.Count > 0 && res.CanBeActivated;

            Logger.Debug($"Ingress connectivity request. Active: {isActive}");
            Sender.Tell(new IngressConnectivityResponse(isActive));
        });

        // Internal request from controller to notify ingress
        // Auto send to ingress when shard status changes
        Receive<IngressConnectionCanActivated>(msg =>
        {
            var isOnline = msg.IsShardOnline ? "online" : "offline";
            Logger.Debug($"Auto send to shard is {isOnline} and ingress will be notified!");
            Context.PubSub().Ingress.Publish(new NotifyIngressShardIsOnline(msg.IsShardOnline));
        });
    }

    private void HandleClusterEvents()
    {
        Receive<IncreaseClusterMember>(msg =>
        {
            Logger.Debug("Increase counter");
            _activeIngress.Add(msg.ClusterMemberRef);
            SendCountUpdate(new IngressConnectionUpdateMessage(_activeIngress.Count > 0));
        });

        Receive<DecreaseClusterMember>(msg =>
        {
            Logger.Debug("Decrease counter");
            _activeIngress.Remove(msg.ClusterMemberRef);
            SendCountUpdate(new IngressConnectionUpdateMessage(_activeIngress.Count > 0));
        });

        /*Receive<IngressConnectivityRequest>(msg =>
            Sender.Tell(new IngressConnectionUpdateMessage(_activeIngress.Count > 0)));*/
    }
}
