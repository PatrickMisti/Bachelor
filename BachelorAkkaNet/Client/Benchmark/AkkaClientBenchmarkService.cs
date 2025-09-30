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

namespace Client.Benchmark;

internal class AkkaClientBenchmarkService : IBenchmarkService, IMetricsSink
{
    public event Action<MetricsUpdate>? Metrics;
    public event Action<MetricsSnapshot>? ClusterNodes;

    private readonly ActorSystem _actorSystem;
    private IActorRef? _apiController;
    private readonly ILoggingAdapter _logger;

    private readonly CancellationToken _cancellationToken;
    private int _nodes;
    private const string ClusterSystem = "cluster-system";

    private readonly ConcurrentQueue<MetricsSample> _samples = new();
    private long _windowMessages;
    private long _windowErrors;
    private readonly Stopwatch _uptime = Stopwatch.StartNew();
    private DateTime _windowStart = DateTime.UtcNow;

    public AkkaClientBenchmarkService(CancellationToken stop)
    {
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
        var proxyController = _actorSystem.CreateProxy(ClusterMemberRoles.Controller);

        var proxyIngress = _actorSystem.CreateProxy(ClusterMemberRoles.Ingress);

        _apiController = _actorSystem.ActorOf(ApiController.Prop(proxyController, proxyIngress, this), "api-controller");

        _ = Task.Run(AggregationLoopAsync, _cancellationToken);

        return Task.CompletedTask;
    }

    private async Task AggregationLoopAsync()
    {
        var latencies = new List<double>(1024);

        while (!_cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(1000, _cancellationToken);

            // alle Samples der letzten Sekunde leeren
            while (_samples.TryDequeue(out var s))
                latencies.Add(s.LatencyMs);

            var now = DateTime.UtcNow;
            var seconds = Math.Max((now - _windowStart).TotalSeconds, 1.0);
            var msgs = Interlocked.Exchange(ref _windowMessages, 0);
            var errs = Interlocked.Exchange(ref _windowErrors, 0);
            var tps = msgs / seconds;

            latencies.Sort();
            double P(double p) =>
                latencies.Count == 0 ? 0 :
                    latencies[(int)Math.Clamp(Math.Round((latencies.Count - 1) * p), 0, latencies.Count - 1)];

            var update = new MetricsUpdate(
                ThroughputPerSec: tps,
                Latencies: latencies.ToArray(),
                ErrorPercent: msgs == 0 ? 0 : (double)errs / msgs,
                Messages: (int)msgs,
                RunningFor: _uptime.Elapsed,
                Cluster: null
            );

            Metrics?.Invoke(update);

            latencies.Clear();
            _windowStart = now;
        }
    }

    public async Task CheckMeasuringAsync()
    {
        await _apiController.Ask<AskForRaceSessionsResponse>(AskForRaceSessionsRequest.Instance);
        _logger.Info("Run measuring ask for race sessions");
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

    public void Publish(MetricsSample sample)
    {
        _samples.Enqueue(sample);
        Interlocked.Add(ref _windowMessages, sample.Messages);
        if (!sample.Success) Interlocked.Add(ref _windowErrors, sample.Messages);
    }
}