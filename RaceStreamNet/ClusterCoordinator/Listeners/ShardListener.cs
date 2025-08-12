using Akka.Actor;
using Akka.Cluster;
using Akka.Event;
using Akka.Hosting;
using Infrastructure.Cluster.Messages;

namespace ClusterCoordinator.Listeners;

public class ShardListener : ReceiveActor
{
    private readonly ILoggingAdapter _logger = Context.GetLogger();
    private readonly string _backendRole = "backend";
    private readonly IActorRef _controller;

    private int _count;
    private readonly HashSet<Address> _activeBackends = new();


    public ShardListener(IRequiredActor<ClusterController> controller)
    {
        _controller = controller.ActorRef;

        Receive<ClusterEvent.MemberUp>(msg =>
        {
            if (!msg.Member.HasRole(_backendRole)) return;

            var address = msg.Member.Address;
            if (_activeBackends.Add(address))
            {
                _count++;
                _logger.Info("Backend joined: {0} | Online Backends: {1}", address, _activeBackends.Count);
                _controller.Tell(new ShardCountUpdateMessage(_count));
            }
        });

        Receive<ClusterEvent.MemberRemoved>(msg =>
        {
            var address = msg.Member.Address;
            if (_activeBackends.Remove(address))
            {
                _count--;
                _logger.Warning("Backend left: {0} | Online Backends: {1}", address, _activeBackends.Count);
                _controller.Tell(new ShardCountUpdateMessage(_count));
            }
        });

        Receive<ClusterEvent.CurrentClusterState>(state =>
        {
            var backends = state.Members
                .Where(m => m.HasRole(_backendRole) && m.Status == MemberStatus.Up)
                .Select(m => m.Address);

            foreach (var address in backends)
                _activeBackends.Add(address);

            _count = _activeBackends.Count();
            _logger.Info("Cluster state initialized. Online Backends: {0}", _activeBackends.Count);
            _controller.Tell(new ShardCountUpdateMessage(_count));
        });
    }

    protected override void PreStart()
    {
        base.PreStart();
        _logger.Debug("ShardListener PreStart");

        // Cluster Subscription
        Cluster.Get(Context.System)
            .Subscribe(
                Self, 
                ClusterEvent.SubscriptionInitialStateMode.InitialStateAsSnapshot,
            typeof(ClusterEvent.IMemberEvent));
    }

    protected override void PostStop()
    {
        _logger.Debug("ShardListener PostStop");

        Cluster.Get(Context.System).Unsubscribe(Self);
        base.PostStop();
    }
}