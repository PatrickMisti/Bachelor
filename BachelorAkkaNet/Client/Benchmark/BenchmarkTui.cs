using System.Collections;
using System.Collections.Concurrent;
using Client.Utility;
using Infrastructure.General;
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

    public List<(bool selected, RaceSession race)>? Sessions;

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


        if (e.Cluster is { } c)
        {
            _metrics.Nodes = c.Nodes;
            _metrics.Shards = c.Shards;
            _metrics.Entities = c.Entities;
            _metrics.ShardDist = c.ShardDistribution;
            _metrics.PipelineMode = c.PipelineMode;
        }

        MetricsSnapshot.Update(_metrics);
    }

    private void OnNodes(MetricsSnapshot snapshot)
    {
        _metrics.Nodes = snapshot.Nodes;
        _metrics.Shards = snapshot.Shards;
        _metrics.Entities = snapshot.Entities;
        _metrics.ShardDist = snapshot.ShardDist;
        _metrics.PipelineMode = snapshot.PipelineMode;

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
                case ConsoleKey.UpArrow:
                    MoveSelection(-1);
                    break;
                case ConsoleKey.DownArrow:
                    MoveSelection(+1);
                    break;
                case ConsoleKey.S:
                    if (Sessions is not null && Sessions.Count > 0)
                    {
                        var race = Sessions.First(x => x.selected).race;
                        await _service.StartSelectedRace(race);
                    }
                    break;
                case ConsoleKey.D:
                    if (Sessions is not null && Sessions.Count > 0)
                    {
                        var race = Sessions.First(x => x.selected).race;
                        await _service.StartSelectedRaceByRegion(race);
                    }
                    break;
                case ConsoleKey.F5:
                    await _service.CheckConnectionsAsync();
                    break;
                case ConsoleKey.F6:
                    var result = await _service.CheckMeasuringAsync();
                    Sessions = result?.Select((x,index) => (index == 0, x)).ToList() ?? null;
                    break;
                case ConsoleKey.Escape: 
                    _service.Stop();
                    return;
            }
        }
    }

    private void MoveSelection(int delta)
    {
        if (Sessions is null || Sessions.Count == 0) return;

        var index = Sessions.FindIndex(x => x.selected);

        if (index < 0)
        {
            var first = Sessions[0];
            first.selected = true;
            Sessions[0] = first;
            return;
        }

        var next = index + delta;

        if (next < 0 || next >= Sessions.Count) return;

        var cur = Sessions[index];
        cur.selected = false;
        Sessions[index] = cur;

        var tgt = Sessions[next];
        tgt.selected = true;
        Sessions[next] = tgt;
    }
}