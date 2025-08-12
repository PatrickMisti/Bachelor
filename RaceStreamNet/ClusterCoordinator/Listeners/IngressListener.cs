using Akka.Actor;
using Akka.Cluster;

namespace ClusterCoordinator.Listeners;

public class IngressListener : ReceiveActor
{

    public IngressListener()
    {

    }

    protected override void PreStart()
    {
        base.PreStart();

        // Subscribe to cluster events
        Cluster
            .Get(Context.System)
            .Subscribe(
                Self, 
                ClusterEvent.SubscriptionInitialStateMode.InitialStateAsSnapshot,
                typeof(ClusterEvent.IMemberEvent));
    }

    protected override void PostStop()
    {
        base.PostStop();
        // Unsubscribe from cluster events
        Cluster.Get(Context.System).Unsubscribe(Self);
    }
}