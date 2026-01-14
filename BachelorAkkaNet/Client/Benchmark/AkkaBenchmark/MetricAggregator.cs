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
    private long _winMsgs, _winErrs;
    private readonly Stopwatch _uptime = Stopwatch.StartNew();
    private DateTime _windowStart = DateTime.UtcNow;

    public event Action<MetricsUpdate>? OnUpdate;
    public event Action<DriverInfoState> ? OnDriverInfoUpdate;
    private readonly Task _loop;

    public MetricAggregator()
    {
        _loop = Task.Run(AggregationLoopAsync);
    }

    private async Task AggregationLoopAsync()
    {
        var reader = _channel.Reader;
        var tick = Task.Delay(1000);

        while (true)
        {
            // Drain all available events
            while (reader.TryRead(out var ev)) Consume(ev);

            // Emit once per second
            if (tick.IsCompleted)
            {
                EmitWindow();
                tick = Task.Delay(1000);
            }

            // If writer is completed and buffer is empty, flush last window and exit
            if (reader.Completion.IsCompleted && !reader.TryRead(out _))
            {
                if (_lat.Count > 0 || Volatile.Read(ref _winMsgs) > 0 || Volatile.Read(ref _winErrs) > 0)
                    EmitWindow();
                break;
            }

            // Wait for either new data or next tick
            if (!reader.TryRead(out _))
                await Task.WhenAny(reader.WaitToReadAsync().AsTask(), tick);
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
                // no-op (kept for potential future gauges)
                break;

            case StreamBatch b:
                _lat.Add(b.LatencyMs);
                Interlocked.Add(ref _winMsgs, b.Count);
                if (!b.Success) Interlocked.Add(ref _winErrs, b.Count);
                break;

            case StreamEnded end:
                if (!end.Success) Interlocked.Increment(ref _winErrs);
                break;

            case DriverInfoState state:
                try
                {
                    OnDriverInfoUpdate?.Invoke(state);
                }
                catch
                {
                    // Avoid subscriber exceptions killing the loop
                }
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

        var update = new MetricsUpdate(
            ThroughputPerSec: tps,
            Latencies: _lat.ToArray(),
            // Emit as percentage (0..100) so the TUI prints correct values like "5.00%"
            ErrorPercent: msgs == 0 ? 0 : (double)errs / msgs * 100.0,
            Messages: (int)msgs,
            RunningFor: _uptime.Elapsed
        );

        try
        {
            OnUpdate?.Invoke(update);
        }
        catch
        {
            // Avoid subscriber exceptions killing the loop
        }

        _lat.Clear();
        _windowStart = now;
    }

    public void Publish(IMetricEvent ev) => _channel.Writer.TryWrite(ev);

    public void Dispose()
    {
        // Complete the writer so the loop drains remaining events and exits
        _channel.Writer.TryComplete();
        try
        {
            _loop.Wait();
        }
        catch
        {
            // ignored
        }
    }
}