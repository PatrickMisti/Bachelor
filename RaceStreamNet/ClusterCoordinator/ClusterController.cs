using Akka.Actor;
using Akka.Cluster;
using Akka.Event;
using Akka.Persistence;
using Infrastructure.Cluster.Messages;

namespace ClusterCoordinator;

public class ClusterController : ReceivePersistentActor
{
    public override string PersistenceId => "cluster-controller";
    private readonly ILoggingAdapter _logger = Context.GetLogger();

    private IActorRef? _shardRef;
    private bool _hasShardRegion = false;

    public ClusterController()
    {
        _logger.Info("ClusterController started");

        

        Command<RegisterShardResponse>(msg =>
        {
            // todo error handling
            // Handle the response from the shard registration
            Persist(new RegisteredShard(msg.ShardRef!.Path.ToString()), e =>
            {
                _shardRef = msg.ShardRef;
                Context.Watch(_shardRef);
                _logger.Info("Shard registered: {Path}", _shardRef.Path);
            });
        });

        Command<Terminated>(msg =>
        {
            if (msg.ActorRef.Equals(_shardRef))
            {
                _logger.Warning("Shard terminated: {Path}", msg.ActorRef.Path);
                _shardRef = null;
            }
        });

        Command<ClusterEvent.MemberUp>(msg =>
        {
            if (msg.Member.HasRole("backend"))
            {
                _logger.Info("Member up with role = {Role}, IP = {Address}", msg.Member.Roles, msg.Member.Address);
            }
        });

        Command<ShardCountUpdateMessage>(msg =>
        {
            Persist(msg, e =>
            {
                _hasShardRegion = msg.Count < 0;
                _logger.Debug("Is ShardRegion register {0}", _hasShardRegion);
            });
        });

        HandleRecovery();
    }


    private void HandleRecovery()
    {
        Recover<ShardCountUpdateMessage>(msg =>
        {
            _hasShardRegion = msg.Count < 0;
            _logger.Debug("Recover shard {0}", _hasShardRegion);
        });

        Recover<RegisteredShard>(msg =>
        {
            _shardRef = Context.ActorSelection(msg.Path).ResolveOne(TimeSpan.FromSeconds(3)).Result;
            _logger.Info("Shard region restarted");
        });

    }

    protected override void PreStart()
    {
        base.PreStart();
        _logger.Debug("ClusterController PreStart");

        // Cluster Subscription
        Cluster.Get(Context.System).Subscribe(Self, ClusterEvent.SubscriptionInitialStateMode.InitialStateAsEvents,
            typeof(ClusterEvent.IMemberEvent));
    }

    protected override void PostStop()
    {
        _logger.Debug("ClusterController PostStop");
        // Unsubscribe from cluster events
        Cluster.Get(Context.System).Unsubscribe(Self);

        base.PostStop();
    }
}