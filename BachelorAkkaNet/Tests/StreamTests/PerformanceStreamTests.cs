using Akka;
using Akka.Event;
using Akka.Streams;
using Akka.Streams.Dsl;
using Akka.TestKit.Xunit2;
using Infrastructure.Http;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Tests.StreamTests.Assets;
using Tests.Utilities;
using Xunit;
using Xunit.Abstractions;
using TimeSpan = System.TimeSpan;

namespace Tests.StreamTests;

public class PerformanceStreamTests(ITestOutputHelper helper) : TestKit(TestConfig, helper)
{
    private static readonly string TestConfig = """
                                                akka {
                                                  loglevel = "INFO"                         # nur INFO+ überhaupt posten
                                                  stdout-loglevel = "INFO"
                                                  loggers = [
                                                    "Akka.TestKit.TestEventListener, Akka.TestKit",   # -> xUnit-Output via helper
                                                    "Akka.Event.StandardOutLogger, Akka"              # (optional) zusätzlich Konsole
                                                  ]
                                                }
                                                """;

    private ILoggingAdapter Logger => Sys.Log;

    [Fact]
    public async Task Throughput_should_be_higher_with_fast_sink_than_slow_sink()
    {
        var mat = Sys.Materializer();
        const int n = 2000;

        // Fast sink
        var fastCount = 0;
        var swFast = Stopwatch.StartNew();

        await Source.Repeat(1)
            .Take(n)
            .ToMaterialized(Sink.ForEach<int>(_ => Interlocked.Increment(ref fastCount)), Keep.Right)
            .Run(mat);

        swFast.Stop();
        var fastTps = fastCount / swFast.Elapsed.TotalSeconds;

        // Slow sink
        var slowCount = 0;
        var swSlow = Stopwatch.StartNew();

        await Source.Repeat(1)
            .Take(50) // take only 50 to keep test time reasonable
            .ToMaterialized(Sink.ForEachAsync<int>(1, async _ =>
            {
                await Task.Delay(100);
                Interlocked.Increment(ref slowCount);
            }), Keep.Right)
            .Run(mat);

        swSlow.Stop();
        var slowTps = slowCount / swSlow.Elapsed.TotalSeconds;

        Assert.True(fastTps > slowTps * 5, $"expected fast throughput > 5x slow; fast={fastTps:F1}/s slow={slowTps:F1}/s");
    }

    [Fact]
    public async Task Latency_should_reflect_processing_delay()
    {
        var mat = Sys.Materializer();
        var latSumMs = 0.0;
        var count = 0;

        await Source.Repeat(1)
            .Take(30)
            .Select(_ => DateTime.UtcNow)
            .SelectAsync(1, async x =>
            {
                // simulate some processing delay
                await Task.Delay(50);
                return x;
            })
            .ToMaterialized(Sink.ForEach<DateTime>(created =>
            {
                var l = (DateTime.UtcNow - created).TotalMilliseconds;
                Interlocked.Add(ref count, 1);
                // hacky to make double as atomic
                Interlocked.Exchange(
                    // convert double to long bits
                    ref Unsafe.As<double, long>(ref latSumMs),
                    // convert back to double
                    BitConverter.DoubleToInt64Bits(
                        BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(latSumMs)) + l));
            }), Keep.Right)
            .Run(mat);

        // avg latency
        var avgLatency = latSumMs / Math.Max(1, count);
        Logger.Info($"Average latency is : {avgLatency}");

        Assert.True(avgLatency is >= 40 and <= 150, $"avg latency expected ~50ms; got {avgLatency:F1}ms");
    }

    [Fact(Skip = "Local run only - CI may cause troubles")]
    //[Fact]
    public async Task Cpu_usage_should_rise_under_cpu_bound_stage()
    {
        var mat = Sys.Materializer();

        // Warm-up
        await RunCpuTest(mat);
        await RunCpuTest(mat);

        var results = new List<double>();
        for (int i = 0; i < 5; i++)
            results.Add(await RunCpuTest(mat));

        var avg = results.Average();
        var min = results.Min();
        var max = results.Max();

        string result = string.Join(", ", results.Select(r => r.ToString("F1")));
        Logger.Info($"CPU delta results: [{result}], avg={avg:F1}, min={min:F1}, max={max:F1}");

        Assert.True(min > 5, $"expected at least +5% CPU delta, got min {min:F1}%");
    }

    //[Fact]
    [Fact(Skip = "Local run")]
    public async Task Memory_should_peak_with_buffer_when_sink_is_slow()
    {
        var mat = Sys.Materializer();

        var proc = Process.GetCurrentProcess();
        long peakWorkingSet = proc.WorkingSet64;
        long peakManaged = GC.GetTotalMemory(false);
        var g0Start = GC.CollectionCount(0);
        var g1Start = GC.CollectionCount(1);
        var g2Start = GC.CollectionCount(2);

        using var cts = new CancellationTokenSource();

        // calc all 100ms peak memory usage while running
        var sampler = Task.Run(async () =>
        {
            while (!cts.IsCancellationRequested)
            {
                proc.Refresh();
                peakWorkingSet = Math.Max(peakWorkingSet, proc.WorkingSet64);
                peakManaged = Math.Max(peakManaged, GC.GetTotalMemory(false));
                await Task.Delay(100, cts.Token);
            }
        }, cts.Token);

        
        var done =
            Source.Repeat(100_000)
                  .Take(50_000)
                  .Select(sz => new byte[sz])// alloc ~100 KB
                  .Buffer(5000, OverflowStrategy.Backpressure)
                  .Select(b => b.Length)
                  .ToMaterialized(Sink.ForEach<int>(async _ => await Task.Delay(1)), Keep.Right)
                  .Run(mat);

        await done;

        cts.Cancel();
        try { await sampler; } catch { /* ignore */ }

        // Endstände
        proc.Refresh();
        var wsAfter = proc.WorkingSet64;
        var heapAfter = GC.GetTotalMemory(forceFullCollection: true); // finaler Heap nach GC
        var g0 = GC.CollectionCount(0) - g0Start;
        var g1 = GC.CollectionCount(1) - g1Start;
        var g2 = GC.CollectionCount(2) - g2Start;

        // Log
        Logger.Info($"Peak WorkingSet: {peakWorkingSet / 1024d / 1024d:F1} MB, \n" +
                  $"Peak Managed: {peakManaged / 1024d / 1024d:F1} MB, \n" +
                  $"WS After: {wsAfter / 1024d / 1024d:F1} MB, \n" +
                  $"Heap After (post-GC): {heapAfter / 1024d / 1024d:F1} MB, \n" +
                  $"GC Gen0/1/2: {g0}/{g1}/{g2}");

        Logger.Info($"PeakWorkingSet {peakWorkingSet} and WsAfter {wsAfter * 1024 * 1024}");
        // Asserts: Peak muss deutlich > After sein; GC muss gearbeitet haben
        Assert.True(peakWorkingSet > wsAfter + 20 * 1024 * 1024, "erwarte >20 MB Working-Set-Peak");
        Assert.True(g0 > 0, "erwarte mindestens ein Gen0 GC");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    public async Task Parallelism_on_json_workload(int parallelism)
    {
        var mat = Sys.Materializer();
        var mock = MockSpeedTelemetry.GetMockData;

        var res = (JsonSerializer.Deserialize<IReadOnlyList<TelemetryDateDto>>(mock) ?? []).Count();

        const int nMocks = 5_000;
        var sw = Stopwatch.StartNew();

        var processAggregate = await Source
            .Repeat(mock)
            .Take(nMocks)
            .SelectAsync(parallelism, x => Task.FromResult(ParseToDto(x)))
            .Select(_ => 1)
            .RunAggregate(0, (acc, one) => acc + one, mat);

        sw.Stop();
        var mocksPerSec = processAggregate / sw.Elapsed.TotalSeconds;
        var itemsPerSec = processAggregate * res / sw.Elapsed.TotalSeconds;

        Logger.Info($"p={parallelism}: \n{processAggregate} mocks in {sw.Elapsed.TotalSeconds:F2}s --> {mocksPerSec:F0} mocks/s  (~{itemsPerSec:F0} items/s, {res} items/mock)");
    }

    private string ParseToDto(string mock)
    {
        var res = JsonSerializer.Deserialize<IReadOnlyList<TelemetryDateDto>>(mock) ?? new List<TelemetryDateDto>();
        //Logger.Debug($"Deserialize mock data with {res.Count} elements");
        return JsonSerializer.Serialize(res);
    }

    private async Task<double> RunCpuTest(IMaterializer mat)
    {
        // measure baseline CPU
        var baseline = CpuMeasuring.MeasureCpuUsagePercent(TimeSpan.FromMilliseconds(300));
        Logger.Info($"Baseline Cpu measuring: {baseline}");

        // CPU-intensive stage (intentionally synchronous and heavy)
        var done = Source.Repeat(1)
            .Take(200_000)
            .Select(x =>
            {
                // simulate some CPU work
                var acc = 0.0;
                for (int i = 0; i < 200; i++)
                    acc += Math.Sqrt(i * x + 1);
                return acc;
            })
            .ToMaterialized(Sink.Ignore<double>(), Keep.Right)
            .Run(mat);

        // calculate CPU while under load
        var underLoad = CpuMeasuring.MeasureCpuUsagePercent(TimeSpan.FromMilliseconds(500));
        Logger.Info($"Cpu measuring under load {underLoad}");

        await done;
        return underLoad - baseline;
    }

    [Fact]
    public async Task Backpressure_vs_Drop_throughput_latency_and_losses()
    {
        var mat = Sys.Materializer();
        const int total = 4000;
        const int bufferSize = 50;
        const int workMs = 2;


        async Task<CpuMeasuring.LatencyThroughputResult> RunOverflowScenario(OverflowStrategy strategy)
        {
            var latencies = new ConcurrentBag<double>();
            var elements = 0;

            var timeResult = await CpuMeasuring.MeasureLatency(async () =>
            {
                await Source.From(Enumerable.Range(0, total))
                    .Select(_ => DateTime.UtcNow)
                    .Buffer(bufferSize, strategy)
                    .SelectAsync(1, async x =>
                    {
                        // simulate some processing delay
                        await Task.Delay(workMs);
                        var l = (DateTime.UtcNow - x).TotalMilliseconds;
                        latencies.Add(l);
                        Interlocked.Increment(ref elements);
                        return NotUsed.Instance;
                    })
                    .RunWith(Sink.Ignore<NotUsed>(), mat);
            });

            var latSort = latencies.ToList();
            latSort.Sort();


            return new CpuMeasuring.LatencyThroughputResult(
                ListSize: elements,
                Rate: elements / (timeResult / 1000.0), // calc rate per second
                P50: CpuMeasuring.Percentile(latSort, .5),
                P95: CpuMeasuring.Percentile(latSort, .95),
                P99: CpuMeasuring.Percentile(latSort, .99));
        }

        var backpressure = await RunOverflowScenario(OverflowStrategy.Backpressure);
        var dropNew = await RunOverflowScenario(OverflowStrategy.DropNew);
        var dropHead = await RunOverflowScenario(OverflowStrategy.DropHead);
        var dropTail = await RunOverflowScenario(OverflowStrategy.DropTail);

        Sys.Log.Warning("Strategy       | Rate/s | Count | P50 ms | P95 ms | P99 ms");
        Sys.Log.Warning("---------------|--------|-------|--------|--------|-------");
        Sys.Log.Warning($"Backpressure   | {backpressure.Rate,6:F1} | {backpressure.ListSize,5} | {backpressure.P50,6:F1} | {backpressure.P95,6:F1} | {backpressure.P99,5:F1}");
        Sys.Log.Warning($"DropNew        | {dropNew.Rate,6:F1} | {dropNew.ListSize,5} | {dropNew.P50,6:F1} | {dropNew.P95,6:F1} | {dropNew.P99,5:F1}");
        Sys.Log.Warning($"DropHead       | {dropHead.Rate,6:F1} | {dropHead.ListSize,5} | {dropHead.P50,6:F1} | {dropHead.P95,6:F1} | {dropHead.P99,5:F1}");
        Sys.Log.Warning($"DropTail       | {dropTail.Rate,6:F1} | {dropTail.ListSize,5} | {dropTail.P50,6:F1} | {dropTail.P95,6:F1} | {dropTail.P99,5:F1}");

        Assert.True(Math.Abs(backpressure.ListSize - total) == 0, "Backpressure should keep all messages");
        Assert.True(dropNew.ListSize < total, "DropNew should lose messages");
        Assert.True(dropHead.ListSize < total, "DropHead should lose messages");
        Assert.True(dropTail.ListSize < total, "DropTail should lose messages");
    }
}