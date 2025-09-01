using Akka.Actor;
using Akka.Cluster;
using Akka.Event;
using Akka.Hosting;
using ClusterCoordinator.Actors.Messages.ClusterMemberEvents;
using Infrastructure.General;

namespace ClusterCoordinator.Actors.Listeners;

public class ClusterEventListener : ReceiveActor
{
    private readonly ILoggingAdapter _log = Context.GetLogger();

    private readonly Dictionary<ClusterMemberEnum, HashSet<IActorRef>> _targets = new();

    public ClusterEventListener(IRequiredActor<ShardListener> shardListenerRef, IRequiredActor<IngressListener> ingressListenerRef)
    {
        _log.Info("Start ClusterEventListener ");
        Add(ClusterMemberEnum.Backend, shardListenerRef.ActorRef);
        Add(ClusterMemberEnum.Ingress, ingressListenerRef.ActorRef);


        Receive<ClusterEvent.MemberUp>(ForwardIfMatch);
        Receive<ClusterEvent.MemberRemoved>(ForwardIfMatch);
        Receive<ClusterEvent.UnreachableMember>(ForwardIfMatch);
        Receive<ClusterEvent.ReachableMember>(ForwardIfMatch);
    }

    private void ForwardIfMatch(ClusterEvent.IMemberEvent evt)
    {
        switch (evt)
        {
            case ClusterEvent.MemberUp u:
                _log.Info($"Cluster Member is Up {u}, {string.Join(',', u.Member.Roles)}");
                foreach (var s in u.Member.Roles)
                    Broadcast(ClusterMemberExtension.Parse(s), new IncreaseClusterMember(u.Member.Address));
                break;

            case ClusterEvent.MemberRemoved r:
                _log.Info($"Cluster Member is Removed {r}, {string.Join(',', r.Member.Roles)}");
                foreach (var s in r.Member.Roles)
                    Broadcast(ClusterMemberExtension.Parse(s), new DecreaseClusterMember(r.Member.Address));
                break;
        }
    }

    private void ForwardIfMatch(ClusterEvent.IReachabilityEvent evt)
    {
        switch (evt)
        {
            case ClusterEvent.UnreachableMember u:
                _log.Info($"Cluster Member is unreachable {u}, {string.Join(',', u.Member.Roles)}");
                foreach (var s in u.Member.Roles)
                    Broadcast(ClusterMemberExtension.Parse(s), new DecreaseClusterMember(u.Member.Address));
                break;

            case ClusterEvent.ReachableMember r:
                _log.Info($"Cluster Member is unreachable {r}, {string.Join(',', r.Member.Roles)}");
                foreach (var s in r.Member.Roles)
                    Broadcast(ClusterMemberExtension.Parse(s), new IncreaseClusterMember(r.Member.Address));
                break;
        }
    }

    private void Add(ClusterMemberEnum role, IActorRef target)
    {
        if (!_targets.TryGetValue(role, out var set))
        {
            set = new HashSet<IActorRef>();
            _targets[role] = set;
        }
        if (set.Add(target))
            _log.Info("Added target {0} for role '{1}'", target.Path, role);
    }


    private void Broadcast(ClusterMemberEnum role, IUpdateClusterCount value)
    {
        if (_targets.TryGetValue(role, out var targets))
        {
            foreach (var t in targets)
                t.Tell(value, Self);
        }
    }

    protected override void PreStart()
    {
        base.PreStart();
        _log.Debug("ClusterEvent PreStart");

        // Cluster Subscription
        Cluster.Get(Context.System)
            .Subscribe(
                Self,
                ClusterEvent.SubscriptionInitialStateMode.InitialStateAsEvents,
                typeof(ClusterEvent.IMemberEvent),
                typeof(ClusterEvent.IReachabilityEvent));
    }

    protected override void PostStop()
    {
        _log.Debug("ClusterEvent PostStop");

        Cluster.Get(Context.System).Unsubscribe(Self);
        base.PostStop();
    }

    protected override void PreRestart(Exception reason, object message)
    {
        base.PreRestart(reason, message);

        _log.Info($"Restart ClusterEventListener because of exception {reason.Message}");
    }
}