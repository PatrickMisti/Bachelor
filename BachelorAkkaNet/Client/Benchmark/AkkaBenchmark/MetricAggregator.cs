using Client.Utility;
using System.Diagnostics;
using System.Threading.Channels;

namespace Client.Benchmark.AkkaBenchmark;

internal sealed class MetricAggregator : IMetricsPublisher, IDisposable
{
    private readonly Channel<IMetricEvent> _channel =
        Channel.CreateUnbounded<IMetricEvent>(new UnboundedChannelOptions
            { SingleReader = true, SingleWriter = false });

    
    private readonly List<double> _lat = new(1024);
    private long _winMsgs, _winErrs, _activeStreams;
    private readonly Stopwatch _uptime = Stopwatch.StartNew();
    private DateTime _windowStart = DateTime.UtcNow;

    public event Action<MetricsUpdate>? OnUpdate;

    private readonly CancellationTokenSource _cts = new();
    private readonly Task _loop;

    public MetricAggregator()
    {
        _loop = Task.Run(AggregationLoopAsync);
    }

    private async Task AggregationLoopAsync()
    {
        var reader = _channel.Reader;
        var tick = Task.Delay(1000, _cts.Token);

        while (!_cts.IsCancellationRequested)
        {
            while (reader.TryRead(out var ev)) Consume(ev);

            if (tick.IsCompleted)
            {
                EmitWindow();
                tick = Task.Delay(1000, _cts.Token);
            }

            // kleine Blockade, wenn gerade nichts da ist
            if (!reader.TryRead(out _))
                await Task.WhenAny(reader.WaitToReadAsync(_cts.Token).AsTask(), tick);
        }
    }

    private void Consume(IMetricEvent ev)
    {
        switch (ev)
        {
            case ReqEnd req:
                _lat.Add(req.LatencyMs);
                Interlocked.Add(ref _winMsgs, req.Messages);
                if (!req.Success) Interlocked.Add(ref _winErrs, req.Messages);
                break;

            case StreamStarted:
                Interlocked.Increment(ref _activeStreams);
                break;

            case StreamBatch b:
                _lat.Add(b.LatencyMs);
                Interlocked.Add(ref _winMsgs, b.Count);
                if (!b.Success) Interlocked.Add(ref _winErrs, b.Count);
                break;

            case StreamEnded end:
                Interlocked.Decrement(ref _activeStreams);
                if (!end.Success) Interlocked.Increment(ref _winErrs);
                break;

            case CustomMetric:
                // Hier könntest du eine Registry/Gauges pflegen
                break;
        }
    }

    private void EmitWindow()
    {
        var now = DateTime.UtcNow;
        var seconds = Math.Max((now - _windowStart).TotalSeconds, 1.0);

        var msgs = Interlocked.Exchange(ref _winMsgs, 0);
        var errs = Interlocked.Exchange(ref _winErrs, 0);
        var tps = msgs / seconds;

        _lat.Sort();
        double P(double p) =>
            _lat.Count == 0 ? 0 :
                _lat[(int)Math.Clamp(Math.Round((_lat.Count - 1) * p), 0, _lat.Count - 1)];

        var update = new MetricsUpdate(
            ThroughputPerSec: tps,
            Latencies: _lat.ToArray(),
            ErrorPercent: msgs == 0 ? 0 : (double)errs / msgs,
            Messages: (int)msgs,
            RunningFor: _uptime.Elapsed
        );

        OnUpdate?.Invoke(update);

        _lat.Clear();
        _windowStart = now;
    }

    public void Publish(IMetricEvent ev)
    {
        _channel.Writer.TryWrite(ev);
    }

    public void Dispose()
    {
        _cts.Cancel();
        try
        {
            _loop.Wait();
        }
        catch
        {
            // ignored
        }
        _cts.Dispose();
    }
}