using Akka.Actor;
using Akka.Cluster.Tools.PublishSubscribe;
using Akka.Configuration;
using Akka.Event;
using Akka.Serialization;
using Client.AkkaTools;
using Client.Benchmark.Actors;
using Client.Benchmark.Actors.Messages;
using Client.Utility;
using Infrastructure.General;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Client.Benchmark.AkkaBenchmark;

internal class AkkaClientBenchmarkService : IBenchmarkService, IMetricsPublisher, IDisposable
{
    public event Action<MetricsUpdate>? Metrics;
    public event Action<MetricsSnapshot>? ClusterNodes;

    private readonly ActorSystem _actorSystem;
    private IActorRef? _apiController;
    private readonly ILoggingAdapter _logger;

    private readonly CancellationToken _cancellationToken;

    private const string ClusterSystem = "cluster-system";

    private readonly MetricAggregator _agg = new();

    public AkkaClientBenchmarkService(CancellationToken stop)
    {
        _cancellationToken = stop;

        _actorSystem = ActorSystem.Create(
            ClusterSystem,
            ConfigurationFactory
                .ParseString(HoconGenerator.Hocon)
                .WithFallback(DistributedPubSub.DefaultConfig())
                .WithFallback(ConfigurationFactory.Default())
                .WithFallback(HyperionSerializer.DefaultConfiguration()));

        _logger = Logging.GetLogger(_actorSystem, typeof(AkkaClientBenchmarkService));
        _agg.OnUpdate += u => Metrics?.Invoke(u);
    }

    public Task StartAsync()
    {
        var proxyController = _actorSystem.CreateProxy(ClusterMemberRoles.Controller);
        var proxyIngress = _actorSystem.CreateProxy(ClusterMemberRoles.Ingress);

        _apiController = _actorSystem.ActorOf(ApiController.Prop(proxyController, proxyIngress, this), "api-controller");
        return Task.CompletedTask;
    }

    public async Task<IEnumerable<RaceSession>?> CheckMeasuringAsync()
    {
        var result = await _apiController!.Ask<AskForRaceSessionsResponse>(AskForRaceSessionsRequest.Instance);
        _logger.Info("Run measuring ask for race sessions");
        return result.ErrorMessage is null ? result.Sessions : null;
    }

    public async Task CheckConnectionsAsync()
    {
        var result = await _apiController.Ask<AskForNodesInClusterResponse>(AskForNodesInClusterRequest.Instance);
        var nodes = result.Nodes;
        var metric = MetricsSnapshot.Current;

        _logger.Debug("Connected to cluster with {0} nodes", nodes);

        const string typeName = "driver";

        AskForClusterStatsResponse? statsResp;
        try
        {
            statsResp = await _apiController.Ask<AskForClusterStatsResponse>(
                new AskForClusterStatsRequest(typeName), TimeSpan.FromSeconds(5));
            _logger.Debug("Grab stats from cluster!");
        }
        catch (AskTimeoutException)
        {
            // Fallback, wenn die Region (noch) nicht erreichbar ist
            statsResp = new AskForClusterStatsResponse(0, 0, new Dictionary<string, int>(), "N/A");
            _logger.Warning("Could not grab data from shard!");
        }

        statsResp ??=
            new AskForClusterStatsResponse(metric.Shards, metric.Entities, metric.ShardDist, metric.PipelineMode);


        ClusterNodes?.Invoke(new MetricsSnapshot
        {
            Nodes = nodes,
            Shards = statsResp.Shards,
            Entities = statsResp.Entities,
            ShardDist = statsResp.ShardDistribution,
            PipelineMode = statsResp.Pipeline
        });
    }

    public Task StartSelectedRace(RaceSession race)
    {
        var sessionKey = race.SessionKey;

        _logger.Info("Start session {0} for measuring", sessionKey);
        _apiController.Tell(new AskForRaceDataMessage(sessionKey));

        return Task.CompletedTask;
    }

    public Task StartSelectedRaceByRegion(RaceSession race)
    {
        var sessionKey = race.SessionKey;

        _logger.Info("Start session {0} for measuring by region", sessionKey);
        _apiController.Tell(new AskForRaceDataByRegionRequest(sessionKey));
        _ = CheckConnectionsAsync();

        return Task.CompletedTask;
    }

    public void Stop()
    {
        _logger.Info("Stop api benchmark");
        Dispose();
    }

    public void Publish(IMetricEvent ev) => _agg.Publish(ev);

    public void Dispose()
    {
        _actorSystem.Dispose();
        _agg.Dispose();
    }
}