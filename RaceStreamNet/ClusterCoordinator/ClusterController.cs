using Akka.Actor;
using Akka.Event;
using Akka.Hosting;
using Akka.Persistence;
using Akka.Util.Internal;
using ClusterCoordinator.Listeners;
using Infrastructure.Cluster.Messages;

namespace ClusterCoordinator;

public class ClusterController : ReceivePersistentActor
{
    public override string PersistenceId => "cluster-controller";
    private readonly ILoggingAdapter _logger = Context.GetLogger();

    private IActorRef? _shardListener;
    private readonly HashSet<string> _ingressPaths;
    private readonly HashSet<IActorRef> _ingressRef;
    // Watcher
    private readonly Dictionary<string, IActorRef> _ingressByPath = new();
    private bool _hasShardRegion;

    public ClusterController(IRequiredActor<ShardListener> shardListener)
    {
        _logger.Info("ClusterController started");
        _hasShardRegion = false;

        _ingressRef = new ();
        _ingressPaths = new ();

        _shardListener = shardListener.ActorRef;

        if (_shardListener is not null) 
            Context.Watch(_shardListener);

        HandleCommands();
        HandleRecovery();
    }

    private IActorRef StartShardListener()
    {
        var listener = Context.ActorOf(Props.Create(() => new ShardListener(Self)), "shard-listener");
        Context.Watch(listener);
        _logger.Info("ShardListener started at {0}", listener.Path);
        return listener;
    }

    protected override SupervisorStrategy SupervisorStrategy() =>
        new OneForOneStrategy(
            maxNrOfRetries: 10,
            withinTimeMilliseconds: 10000,
            localOnlyDecider: ex =>
            {
                _logger.Error(ex, "ShardListener failed - restarting");
                return Directive.Restart;
            });

    protected override void PreStart()
    {
        base.PreStart();
        _shardListener = StartShardListener();
    }

    private void HandleCommands()
    {
        HandleIngressMessages();

        // Internal update
        Command<ShardCountUpdateMessage>(msg =>
        {
            Persist(msg, e =>
            {
                var wasAvailable = _hasShardRegion;
                _hasShardRegion = e.Count > 0;
                _logger.Info("Has ShardRegion = {0}", _hasShardRegion);

                if (!_hasShardRegion)
                {
                    // Shard down -> close all Ingress connections
                    foreach (var actor in _ingressRef)
                        actor.Tell(new NotifyIngressShardConnectionClose());
                }
                else if (!wasAvailable && _hasShardRegion)
                {
                    // Transition false / true -> open all Ingress connections
                    foreach (var actor in _ingressRef)
                        actor.Tell(new ShardAvailableMessage());
                }
            });
        });

        // if Terminated is called check if one shard is available
        Command<Terminated>(t =>
        {
            if (Equals(t.ActorRef, _shardListener))
            {
                _logger.Warning("ShardListener terminated.");
                return;
            }

            var dead = t.ActorRef;
            _ingressRef.Remove(dead);

            var kv = _ingressByPath.FirstOrDefault(p => Equals(p.Value, dead));
            if (!string.IsNullOrEmpty(kv.Key))
            {
                _ingressByPath.Remove(kv.Key);
                _logger.Warning("Ingress terminated: {0}", kv.Key);
                // todo maybe re-resolve Ingress
            }
        });
    }

    public void HandleIngressMessages()
    {
        // Outside from Ingress via proxy or something else
        Command<IngressConnectToClusterMessage>(msg =>
        {
            var path = Sender.Path.ToString();

            Persist(new IngressConnectionRecorded(path), e =>
            {
                _ingressPaths.Add(e.Path);

                if (_ingressRef.Add(Sender))
                {
                    _ingressByPath[path] = Sender;
                    Context.Watch(Sender);
                }

                _logger.Info("Ingress connected to cluster: {0}", path);
            });

            Sender.Tell(_hasShardRegion ? new ShardAvailableMessage()
                : new ShardUnavailableMessage());
        });

        // Internal message to check if Ingress is connected
        Command<IngressResolved>(m =>
        {
            var path = m.Ref.Path.ToString();

            // change the path to the Ingress
            if (_ingressByPath.TryGetValue(path, out var oldRef) && !Equals(oldRef, m.Ref))
            {
                Context.Unwatch(oldRef);
                _ingressRef.Remove(oldRef);
            }

            if (_ingressRef.Add(m.Ref))
            {
                _ingressByPath[path] = m.Ref;
                Context.Watch(m.Ref);
                _logger.Info("Ingress rehydrated: {0}", m.Ref.Path);

                m.Ref.Tell(new IngressConnectionCheckMessage());

                if (_hasShardRegion)
                    m.Ref.Tell(new ShardAvailableMessage());
            }
        });

        // Internal message to check if Ingress is not connected
        Command<IngressResolveFailed>(m =>
        {
            _logger.Warning("Ingress resolve failed for {0}: {1}", m.Path, m.Exception.Message);
        });
    }

    private void HandleRecovery()
    {
        Recover<ShardCountUpdateMessage>(msg =>
        {
            _hasShardRegion = msg.Count > 0;
            _logger.Debug("Recover shard {0}", _hasShardRegion);
        });

        Recover<IngressConnectionRecorded>(msg =>
        {
            _ingressPaths.Add(msg.Path);
            _logger.Info("Ingress recovered path: {0}", msg.Path);
        });
    }

    protected override void OnReplaySuccess()
    {
        _logger.Info("Update ingress reconnect to Coordinator...");
        // Outbound to check if connection to Ingress is up
        _ingressPaths
            .ForEach(
                path => Context
                    .ActorSelection(path)
                    .ResolveOne(TimeSpan.FromMinutes(5))
                    .PipeTo(
                        Self, 
                        success: s => new IngressResolved(s), 
                        failure: f => new IngressResolveFailed(path,f)));
    }
}