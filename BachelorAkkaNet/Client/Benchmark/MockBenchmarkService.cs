using Client.Utility;
using System.Text;

namespace Client.Benchmark;

internal class MockBenchmarkService
{
    public event Action<MetricsUpdate> Metrics;
    public event EventHandler<ClusterEvent>? ClusterEvent;


    private readonly Random _rnd = new();
    private readonly List<double> _lat = new();
    private long _messages;
    private DateTime _start = DateTime.UtcNow;
    private readonly object _guard = new();
    private int _nodes = 3;
    private int _shards = 10;
    private int _entities = 12_000;

    public async Task StartHeartbeatAsync(CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            EmitMetrics(tps: 900 + _rnd.NextDouble() * 400, errors: _rnd.NextDouble() * 0.2);
            await Task.Delay(500, stop);
        }
    }


    public async Task StartWarmupAsync(int durationSec)
    {
        var end = DateTime.UtcNow.AddSeconds(durationSec);
        while (DateTime.UtcNow < end)
        {
            ProduceBatch(200, baseLat: 8, jitter: 4);
            await Task.Delay(100);
        }
    }


    public async Task StartMeasureAsync(int durationSec, int parallelism, int rps)
    {
        var end = DateTime.UtcNow.AddSeconds(durationSec);
        var pacing = TimeSpan.FromMilliseconds(1000.0 / Math.Max(1, rps / Math.Max(1, parallelism)));
        while (DateTime.UtcNow < end)
        {
            for (int i = 0; i < parallelism; i++)
                ProduceBatch(Math.Max(1, rps / Math.Max(1, parallelism)), baseLat: 9 + i, jitter: 6);
            await Task.Delay(pacing);
        }
    }


    public Task TriggerBurstAsync(int count)
    {
        ProduceBatch(count, baseLat: 12, jitter: 15);
        return Task.CompletedTask;
    }


    public async Task RampUpAsync(int startRps, int endRps, int seconds)
    {
        for (int s = 0; s < seconds; s++)
        {
            var rps = startRps + (endRps - startRps) * s / Math.Max(1, seconds - 1);
            ProduceBatch(Math.Max(1, rps / 10), baseLat: 10 + s * 0.2, jitter: 8);
            await Task.Delay(100);
        }
    }

    public Task AddNodeAsync()
    {
        _nodes++;
        ClusterEvent?.Invoke(this, new ClusterEvent("MemberUp", DateTime.Now));
        // simulate rebalance
        _shards++;
        return Task.CompletedTask;
    }


    public Task KillNodeAsync()
    {
        if (_nodes <= 1) return Task.CompletedTask;
        _nodes--;
        ClusterEvent?.Invoke(this, new ClusterEvent("MemberRemoved", DateTime.Now));
        ClusterEvent?.Invoke(this, new ClusterEvent("RebalanceStart", DateTime.Now));
        // spike latencies for a moment
        ProduceBatch(200, baseLat: 50, jitter: 40);
        ClusterEvent?.Invoke(this, new ClusterEvent("RebalanceEnd", DateTime.Now.AddSeconds(2)));
        return Task.CompletedTask;
    }


    public async Task ExportCsvAsync(string filePath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("latency_ms");
        lock (_guard)
            foreach (var l in _lat) sb.AppendLine(l.ToString("F2"));
        await File.WriteAllTextAsync(filePath, sb.ToString());
    }

    private void EmitMetrics(double tps, double errors)
    {
        var lats = Array.Empty<double>();
        lock (_guard)
        {
            if (_lat.Count > 0)
            {
                lats = _lat.TakeLast(Math.Min(100, _lat.Count)).ToArray();
                _lat.Clear();
            }
        }
        var dist = new Dictionary<string, int>();
        for (int i = 1; i <= _nodes; i++) dist[$"n{i}"] = Math.Max(1, _shards / _nodes + (i == 1 ? _shards % _nodes : 0));


        Metrics.Invoke(new MetricsUpdate(
            ThroughputPerSec: tps,
            Latencies: lats,
            ErrorPercent: errors,
            Messages: (int)(tps / 2),
            RunningFor: DateTime.UtcNow - _start,
            Cluster: new ClusterSnapshot(_nodes, _shards, _entities, dist, "Idle")
        ));


        Interlocked.Add(ref _messages, (long)tps);
    }


    private void ProduceBatch(int count, double baseLat, double jitter)
    {
        lock (_guard)
        {
            for (int i = 0; i < count; i++)
            {
                var l = Math.Max(1, baseLat + NextGaussian() * jitter);
                _lat.Add(l);
            }
        }
    }


    private double NextGaussian()
    {
        // Box-Muller
        var u1 = 1.0 - _rnd.NextDouble();
        var u2 = 1.0 - _rnd.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }
}