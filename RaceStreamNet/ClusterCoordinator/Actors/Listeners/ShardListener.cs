using Akka.Actor;
using Akka.Cluster;
using Akka.Event;
using Akka.Hosting;
using ClusterCoordinator.Actors.Listeners.Basis;
using ClusterCoordinator.Actors.Messages;
using ClusterCoordinator.Actors.Messages.ClusterMemberEvents;
using ClusterCoordinator.Actors.Messages.Request;

namespace ClusterCoordinator.Actors.Listeners;

public class ShardListener(IRequiredActor<ClusterController> controller) : DebounceBaseListener(controller.ActorRef)
{
    private readonly HashSet<Address> _activeBackends = new();

    public override void Activated()
    {
        Logger.Info("Activate Handlers in ShardListener!");
        HandleClusterEvents();
    }

    private void HandleClusterEvents()
    {
        Receive<IncreaseClusterMember>(msg =>
        {
            Logger.Debug("Increase counter");
            _activeBackends.Add(msg.ClusterMemberRef);
            SendCountUpdate(new ShardConnectionUpdateMessage(_activeBackends.Count > 0));
        });

        Receive<DecreaseClusterMember>(msg =>
        {
            Logger.Debug("Decrease counter");
            _activeBackends.Remove(msg.ClusterMemberRef);
            SendCountUpdate(new ShardConnectionUpdateMessage(_activeBackends.Count > 0));
        });

        Receive<ShardConnectionRequest>(msg =>
            Sender.Tell(new ShardConnectionUpdateMessage(_activeBackends.Count > 0)));
    }

    protected override void PreStart()
    {
        base.PreStart();
        Logger.Debug("ShardListener PreStart");

        // Cluster Subscription
        Cluster.Get(Context.System)
            .Subscribe(
                Self, 
                ClusterEvent.SubscriptionInitialStateMode.InitialStateAsSnapshot,
            typeof(ClusterEvent.IMemberEvent),
                typeof(ClusterEvent.IReachabilityEvent));
    }

    protected override void PostStop()
    {
        Logger.Debug("ShardListener PostStop");

        Cluster.Get(Context.System).Unsubscribe(Self);
        base.PostStop();
    }
}