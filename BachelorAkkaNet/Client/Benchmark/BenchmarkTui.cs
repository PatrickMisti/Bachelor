using System.Collections.Concurrent;
using Client.Utility;
using Spectre.Console;

namespace Client.Benchmark;

public class BenchmarkTui
{
    private readonly IBenchmarkService _service;
    private readonly CancellationToken _stop;


    private readonly RollingWindow<double> _tpsWindow = new(300);
    private readonly RollingWindow<double> _latWindow = new(1000);
    private readonly ConcurrentQueue<double> _lastLatencies = new();


    private readonly MetricsSnapshot _metrics = new();
    private readonly State _state = new();

    public BenchmarkTui(IBenchmarkService service, CancellationToken stop)
    {
        _service = service;
        _stop = stop;
        _service.Metrics += OnMetrics;
        _service.ClusterNodes += OnNodes;
    }

    private void OnMetrics(MetricsUpdate e)
    {
        // TPS
        _tpsWindow.Add(e.ThroughputPerSec);
        _metrics.Tps = e.ThroughputPerSec;


        // Latencies
        if (e.Latencies.Count > 0)
        {
            foreach (var l in e.Latencies)
            {
                _latWindow.Add(l);
                _lastLatencies.Enqueue(l);
                while (_lastLatencies.Count > 1000) _lastLatencies.TryDequeue(out _);
            }
            var arr = _latWindow.ToArraySorted();
            _metrics.P50 = Percentiles.P(arr, 0.50);
            _metrics.P95 = Percentiles.P(arr, 0.95);
            _metrics.P99 = Percentiles.P(arr, 0.99);
            _metrics.Min = arr.Length > 0 ? arr[0] : 0;
            _metrics.Max = arr.Length > 0 ? arr[^1] : 0;
        }


        _metrics.ErrorsPct = e.ErrorPercent;
        _metrics.MessagesTotal += e.Messages;
        _metrics.TimeRunning = e.RunningFor;


        _metrics.Nodes = e.Cluster?.Nodes ?? _metrics.Nodes;
        _metrics.Shards = e.Cluster?.Shards ?? _metrics.Shards;
        _metrics.Entities = e.Cluster?.Entities ?? _metrics.Entities;
        _metrics.ShardDist = e.Cluster?.ShardDistribution ?? _metrics.ShardDist;
        _metrics.RebalanceStatus = e.Cluster?.RebalanceStatus ?? _metrics.RebalanceStatus;

        MetricsSnapshot.Update(_metrics);
    }

    private void OnNodes(MetricsSnapshot snapshot)
    {
        _metrics.Nodes = snapshot.Nodes;
        _metrics.Shards = snapshot.Shards;
        _metrics.Entities = snapshot.Entities;
        _metrics.ShardDist = snapshot.ShardDist;
        _metrics.RebalanceStatus = snapshot.RebalanceStatus;

        MetricsSnapshot.Update(_metrics);
    }

    public async Task RunAsync()
    {
        var screen = new ScreenView(this);
        var keyTask = Task.Run(HandleKeysAsync, _stop);


        await AnsiConsole.Live(screen.Render())
            .AutoClear(false)
            .Overflow(VerticalOverflow.Ellipsis)
            .Cropping(VerticalOverflowCropping.Top)
            .StartAsync(async ctx =>
            {
                // Initial heartbeat
                _ = _service.StartAsync();

                while (!_stop.IsCancellationRequested)
                {
                    ctx.Refresh();
                    ctx.UpdateTarget(screen.Render());
                    await Task.Delay(100, _stop);
                }
            });


        await keyTask;
    }

    private async Task HandleKeysAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            if (!Console.KeyAvailable)
            {
                await Task.Delay(50, _stop);
                continue;
            }
            var key = Console.ReadKey(intercept: true).Key;
            switch (key)
            {
                /*case ConsoleKey.F5: 
                    _state.Mode = RunMode.Warmup; 
                    await _service.StartWarmupAsync(durationSec: 5); 
                    break;
                case ConsoleKey.F6: _state.Mode = RunMode.Measure; 
                    await _service.StartMeasureAsync(durationSec: 30, parallelism: _state.Parallelism, rps: _state.Rps); 
                    break;
                case ConsoleKey.F7: 
                    await _service.TriggerBurstAsync(count: 1000);
                    break;
                case ConsoleKey.F8:
                    await _service.RampUpAsync(startRps: 100, endRps: 2000, seconds: 20);
                    break;
                case ConsoleKey.N: 
                    await _service.AddNodeAsync(); 
                    break;
                case ConsoleKey.K: 
                    await _service.KillNodeAsync(); 
                    break;
                case ConsoleKey.S: 
                    await _service.ExportCsvAsync("latencies.csv");
                    break;
                case ConsoleKey.UpArrow: 
                    _state.Rps = Math.Min(_state.Rps + 100, 100_000); 
                    break;
                case ConsoleKey.DownArrow: 
                    _state.Rps = Math.Max(_state.Rps - 100, 0); 
                    break;
                case ConsoleKey.RightArrow: 
                    _state.Parallelism = Math.Min(_state.Parallelism + 1, 256); 
                    break;
                case ConsoleKey.LeftArrow: 
                    _state.Parallelism = Math.Max(_state.Parallelism - 1, 1); 
                    break;*/
                case ConsoleKey.F5:
                    await _service.CheckConnectionsAsync();
                    break;
                case ConsoleKey.F6:
                    await _service.CheckMeasuringAsync();
                    break;
                case ConsoleKey.Escape: 
                    _service.Stop();
                    return;
            }
        }
    }
}