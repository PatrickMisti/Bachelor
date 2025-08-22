using Akka.Actor;
using Akka.Cluster;
using Akka.Event;
using Akka.Hosting;
using Infrastructure.Cluster.Messages;

namespace ClusterCoordinator.Listeners;

public class ShardListener : ReceiveActor
{
    private readonly ILoggingAdapter _logger = Context.GetLogger();
    private static string BackendRole => "backend";
    private readonly IActorRef _controller;
    private ICancelable? _debounceTask;

    private readonly HashSet<Address> _activeBackends = new();


    public ShardListener(IActorRef controller)
    {
        _controller = controller;

        HandleClusterEvents();
    }


    private void HandleClusterEvents()
    {
        Receive<ClusterEvent.MemberUp>(msg =>
        {
            if (msg.Member.HasRole(BackendRole) && _activeBackends.Add(msg.Member.Address))
            {
                _logger.Info("Backend joined: {0} | Online Backends: {1}", msg.Member.Address, _activeBackends.Count);
                SendCountUpdate();
            }
        });

        Receive<ClusterEvent.MemberRemoved>(msg =>
        {
            if (_activeBackends.Remove(msg.Member.Address))
            {
                _logger.Warning("Backend removed: {0} | Online Backends: {1}", msg.Member.Address, _activeBackends.Count);
                SendCountUpdate();
            }
        });

        Receive<ClusterEvent.UnreachableMember>(msg =>
        {
            if (msg.Member.HasRole(BackendRole) && _activeBackends.Remove(msg.Member.Address))
            {
                _logger.Warning("Backend unreachable: {0} | Online Backends: {1}", msg.Member.Address, _activeBackends.Count);
                SendCountUpdate();
            }
        });

        Receive<ClusterEvent.ReachableMember>(msg =>
        {
            if (msg.Member.HasRole(BackendRole) && msg.Member.Status == MemberStatus.Up && _activeBackends.Add(msg.Member.Address))
            {
                _logger.Info("Backend reachable again: {0} | Online Backends: {1}", msg.Member.Address, _activeBackends.Count);
                SendCountUpdate();
            }
        });

        Receive<ClusterEvent.CurrentClusterState>(state =>
        {
            _activeBackends.Clear();

            var backends = state.Members
                .Where(m => m.HasRole(BackendRole) && m.Status == MemberStatus.Up)
                .Select(m => m.Address);

            foreach (var address in backends)
                _activeBackends.Add(address);

            _logger.Info("Cluster state initialized. Online Backends: {0}", _activeBackends.Count);
            SendCountUpdate();
        });
    }

    private void SendCountUpdate() => SendCountUpdateDebounced();


    // Send the current count of active backends to the controller
    private void SendCountUpdateWithoutDebounced()
    {
        var count = _activeBackends.Count;
        _logger.Info("Sending shard count update: {0}", count);
        _controller.Tell(new ShardCountUpdateMessage(count));
    }

    // debounced version to avoid flooding the controller with updates
    private void SendCountUpdateDebounced()
    {
        _logger.Info("Debouncing shard count update");
        // Cancel any existing debounce task
        _debounceTask?.Cancel();
        // Schedule a new debounce task
        _debounceTask = Context.System.Scheduler.ScheduleTellOnceCancelable(
            delay: TimeSpan.FromMilliseconds(200),
            receiver: _controller,
            message: new ShardCountUpdateMessage(_activeBackends.Count),
            sender: Self
        );
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
            typeof(ClusterEvent.IMemberEvent),
                typeof(ClusterEvent.IReachabilityEvent));
    }

    protected override void PostStop()
    {
        _logger.Debug("ShardListener PostStop");

        Cluster.Get(Context.System).Unsubscribe(Self);
        base.PostStop();
    }
}