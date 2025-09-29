using Akka.Actor;
using Akka.Cluster.Tools.PublishSubscribe;
using Akka.Cluster.Tools.Singleton;
using Akka.Configuration;
using Akka.Event;
using Akka.Serialization;
using Client.AkkaTools;
using Client.Benchmark.Actors;
using Client.Benchmark.Actors.Messages;
using Client.Utility;
using Infrastructure.General;

namespace Client.Benchmark;

internal class AkkaClientBenchmarkService : IBenchmarkService
{
    public event Action<MetricsUpdate>? Metrics;
    public event Action<MetricsSnapshot>? ClusterNodes;

    private readonly ActorSystem _actorSystem;
    private IActorRef? _apiController;
    private readonly ILoggingAdapter _logger;

    private readonly CancellationToken _cancellationToken;
    private int _nodes;
    private const string ClusterSystem = "cluster-system";

    public AkkaClientBenchmarkService(CancellationToken stop)
    {
        _ = typeof(HyperionSerializer);
        _actorSystem = ActorSystem.Create(
            ClusterSystem,
            ConfigurationFactory
                .ParseString(HoconGenerator.Hocon)
                .WithFallback(DistributedPubSub.DefaultConfig())
                .WithFallback(ConfigurationFactory.Default())
                .WithFallback(HyperionSerializer.DefaultConfiguration()));

        _logger = Logging.GetLogger(_actorSystem, typeof(AkkaClientBenchmarkService));
        _cancellationToken = stop;

        
    }

    public Task StartAsync()
    {
        /*var testMsg = new Infrastructure.PubSub.Messages.NodeInClusterRequest();
        var ser = _actorSystem.Serialization.FindSerializerFor(testMsg);
        _logger.Info("Serializer for NodeInClusterRequest: {0}", ser);*/

        var proxySettings = ClusterSingletonProxySettings
            .Create(_actorSystem)
            .WithRole(ClusterMemberRoles.Controller.ToStr())
            .WithSingletonName(ClusterMemberRoles.Controller.ToStr());

        var proxy = _actorSystem.ActorOf(
            props: ClusterSingletonProxy.Props(
                singletonManagerPath: "/user/controller", 
                settings: proxySettings), 
            name: "controller-api-proxy");

        _apiController = _actorSystem.ActorOf(ApiController.Prop(proxy), "api-controller");

        return Task.CompletedTask;
    }

    public async Task CheckConnectionsAsync()
    {
        var result = await _apiController.Ask<AskForNodesInClusterResponse>(AskForNodesInClusterRequest.Instance);
        _nodes = result.Nodes;
        _logger.Info("Connected to cluster with {0} nodes", _nodes);

        ClusterNodes?.Invoke(new MetricsSnapshot
        {
            Nodes = _nodes,
            Shards = 0,
            Entities = 0,
            ShardDist = new Dictionary<string, int>(),
            RebalanceStatus = "N/A"
        });
    }

    public void Stop()
    {
        _logger.Info("Stop api benchmark");
        _actorSystem.Dispose();
    }
}