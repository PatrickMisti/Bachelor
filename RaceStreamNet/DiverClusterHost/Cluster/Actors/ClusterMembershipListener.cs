using Akka.Actor;
using Akka.Cluster;
using Infrastructure.Cluster.Interfaces;

namespace DiverShardHost.Cluster.Actors;

public class ClusterMembershipListener : ReceiveActor
{
    private readonly ILogger<ClusterMembershipListener> _logger;
    public ClusterMembershipListener(ILogger<ClusterMembershipListener> logger)
    {
        _logger = logger;

        Receive<ClusterEvent.MemberUp>(msg => {
            _logger.LogWarning("Member up with role = {m}, with Ip = {ip}", msg.Member.Roles, msg.Member.Address);
        });
    }

    protected override void PreStart()
    {
        Akka.Cluster.Cluster.Get(Context.System).Subscribe(Self, ClusterEvent.SubscriptionInitialStateMode.InitialStateAsEvents,
            typeof(ClusterEvent.IMemberEvent));
    }

    protected override void PostStop()
    {
        Akka.Cluster.Cluster.Get(Context.System).Unsubscribe(Self);
    }
}