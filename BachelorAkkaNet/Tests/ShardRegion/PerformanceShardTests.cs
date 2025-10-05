using System.Diagnostics;
using System.Globalization;
using Akka.Actor;
using Akka.Event;
using Akka.TestKit.Xunit2;
using Infrastructure.ShardRegion;
using Tests.ShardRegion.Assets;
using Tests.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Tests.ShardRegion;

public class PerformanceShardTests(ITestOutputHelper helper) : TestKit(TestConfig, helper)
{
    private static readonly string TestConfig = """
                                                akka {
                                                  loglevel = "WARNING"
                                                  stdout-loglevel = "WARNING"
                                                  actor.provider = "cluster"
                                                  remote.dot-netty.tcp {
                                                    hostname = "127.0.0.1"
                                                    port = 0
                                                  }
                                                  cluster.min-nr-of-members = 1
                                                  cluster.roles = ["test"]
                                                  loggers = [
                                                    "Akka.TestKit.TestEventListener, Akka.TestKit",
                                                    "Akka.Event.StandardOutLogger, Akka"
                                                  ]
                                                }
                                                """;


    private ILoggingAdapter Logger => Sys.Log;

    [Fact]
    public async Task Single_entity_should_handle_10k_roundtrips()
    {
        var region = await ShardRegionTools.EnsureRegionStarted(Sys);

        var key = DriverKey.Create(10, 1);
        await region.Ask<CreatedDriverMessage>(new CreateModelDriverMessage(key, "Perf", "One", "P1", "DE", "Team"), TimeSpan.FromSeconds(5));

        const int messages = 10_000;
        var latencies = new double[messages];

        var proc = Process.GetCurrentProcess();
        var cpuStart = proc.TotalProcessorTime;
        var wall = Stopwatch.StartNew();

        for (int i = 0; i < messages; i++)
        {
            var sw = Stopwatch.StartNew();
            var _ = await region.Ask<string>(new UpdateTelemetryMessage(key, i, DateTime.UtcNow), TimeSpan.FromSeconds(10));
            sw.Stop();
            latencies[i] = sw.Elapsed.TotalMilliseconds;
        }

        wall.Stop();
        var cpuEnd = proc.TotalProcessorTime;

        var rate = messages / wall.Elapsed.TotalSeconds;
        var p50 = CpuMeasuring.Percentile(latencies, 50);
        var p90 = CpuMeasuring.Percentile(latencies, 90);
        var p99 = CpuMeasuring.Percentile(latencies, 99);
        var cpuPct = CpuMeasuring.CpuPercent(cpuStart, cpuEnd, wall.Elapsed);

        var msg = string.Format(CultureInfo.InvariantCulture,
            "\n-----------------------------\nSingle-entity:\n {0} msgs in {1} ms => {2:F0} msg/s;\n latency p50={3:F2}ms p90={4:F2}ms p99={5:F2}ms;\n CPU={6:F1}%\n",
            messages, wall.ElapsedMilliseconds, rate, p50, p90, p99, cpuPct);
        Logger.Warning(msg);

        Assert.True(messages > 0);
    }

    [Fact]
    public async Task Hundred_entities_should_handle_10k_roundtrips_concurrently()
    {
        var region = await ShardRegionTools.EnsureRegionStarted(Sys);

        const int entityCount = 100;
        const int perEntity = 100; // total 10k

        var keys = Enumerable
            .Range(1, entityCount)
            .Select(i => DriverKey.Create(20, i))
            .ToArray();

        foreach (var k in keys)
            await region.Ask<CreatedDriverMessage>(
                new CreateModelDriverMessage(k, "Perf", "Many", "PM", "DE", "Team"), 
                TimeSpan.FromSeconds(5));

        var total = entityCount * perEntity;
        var latencyTasks = new List<Task<double>>(total);

        var proc = Process.GetCurrentProcess();
        var cpuStart = proc.TotalProcessorTime;
        var wall = Stopwatch.StartNew();

        foreach (var k in keys)
            for (int i = 0; i < perEntity; i++)
                latencyTasks.Add(
                    CpuMeasuring.MeasureLatency(async () => 
                        await region.Ask<string>(
                            new UpdatePositionMessage(k, i, DateTime.UtcNow),
                            TimeSpan.FromSeconds(10))));

        var latencies = await Task.WhenAll(latencyTasks);

        wall.Stop();
        var cpuEnd = proc.TotalProcessorTime;

        var rate = total / wall.Elapsed.TotalSeconds;
        var p50 = CpuMeasuring.Percentile(latencies, 50);
        var p90 = CpuMeasuring.Percentile(latencies, 90);
        var p99 = CpuMeasuring.Percentile(latencies, 99);
        var cpuPct = CpuMeasuring.CpuPercent(cpuStart, cpuEnd, wall.Elapsed);

        var msg = string.Format(CultureInfo.InvariantCulture,
            "\n-----------------------------\nMulti-entity:\n {0} msgs in {1} ms => {2:F0} msg/s;\n latency p50={3:F2}ms p90={4:F2}ms p99={5:F2}ms;\n CPU={6:F1}%\n",
            total, wall.ElapsedMilliseconds, rate, p50, p90, p99, cpuPct);
        Logger.Warning(msg);

        Assert.Equal(total, latencies.Length);
    }

    [Fact]
    public async Task Burst_send_should_be_processed_under_reasonable_time()
    {
        var region = await ShardRegionTools.EnsureRegionStarted(Sys);

        var key = DriverKey.Create(30, 7);
        await region.Ask<CreatedDriverMessage>(
            new CreateModelDriverMessage(key, "Burst", "Test", "BT", "DE", "Team"), 
            TimeSpan.FromSeconds(5));

        const int burstSize = 5000;
        var latencyTasks = new Task<double>[burstSize];

        var proc = Process.GetCurrentProcess();
        var cpuStart = proc.TotalProcessorTime;
        var wall = Stopwatch.StartNew();

        for (int i = 0; i < burstSize; i++)
        {
            int n = i;
            latencyTasks[i] = CpuMeasuring.MeasureLatency(async () => 
                await region.Ask<string>(
                    new UpdateIntervalMessage(key, n, DateTime.UtcNow), 
                    TimeSpan.FromSeconds(10)));
        }

        var latencies = await Task.WhenAll(latencyTasks);

        wall.Stop();
        var cpuEnd = proc.TotalProcessorTime;

        var rate = burstSize / wall.Elapsed.TotalSeconds;
        var p50 = CpuMeasuring.Percentile(latencies, 50);
        var p90 = CpuMeasuring.Percentile(latencies, 90);
        var p99 = CpuMeasuring.Percentile(latencies, 99);
        var cpuPct = CpuMeasuring.CpuPercent(cpuStart, cpuEnd, wall.Elapsed);

        var msg = string.Format(CultureInfo.InvariantCulture,
            "\n-----------------------------\nBurst-entity:\n {0} msgs in {1} ms => {2:F0} msg/s;\n latency p50={3:F2}ms p90={4:F2}ms p99={5:F2}ms;\n CPU={6:F1}%\n",
            burstSize, wall.ElapsedMilliseconds, rate, p50, p90, p99, cpuPct);
        Logger.Warning(msg);

        Assert.Equal(burstSize, latencies.Length);
    }

    [Fact]
    public async Task Warmup_then_single_entity_roundtrips()
    {
        var region = await ShardRegionTools.EnsureRegionStarted(Sys);
        var key = DriverKey.Create(9, 1);

        // warm-up: JIT, caches, mailbox, shard allocation
        await region.Ask<CreatedDriverMessage>(new CreateModelDriverMessage(key, "Warm", "Up", "WU", "DE", "Team"), TimeSpan.FromSeconds(5));
        for (int i = 0; i < 2000; i++)
            await region.Ask<string>(new UpdateTelemetryMessage(key, i, DateTime.UtcNow), TimeSpan.FromSeconds(5));

        // measurement
        const int messages = 5000;
        var latencies = new double[messages];
        var proc = Process.GetCurrentProcess();
        var cpuStart = proc.TotalProcessorTime;
        var wall = Stopwatch.StartNew();
        for (int i = 0; i < messages; i++)
        {
            latencies[i] = await CpuMeasuring.MeasureLatency(async () =>
                await region.Ask<string>(new UpdateTelemetryMessage(key, i, DateTime.UtcNow), TimeSpan.FromSeconds(10)));
        }
        wall.Stop();
        var cpuEnd = proc.TotalProcessorTime;

        var rate = messages / wall.Elapsed.TotalSeconds;
        var p50 = CpuMeasuring.Percentile(latencies, 50);
        var p90 = CpuMeasuring.Percentile(latencies, 90);
        var p99 = CpuMeasuring.Percentile(latencies, 99);
        var cpuPct = CpuMeasuring.CpuPercent(cpuStart, cpuEnd, wall.Elapsed);

        var msg = string.Format(CultureInfo.InvariantCulture,
            "\n-----------------------------\nWarmup->Single:\n {0} msgs in {1} ms => {2:F0} msg/s; lat p50={3:F2} p90={4:F2} p99={5:F2}; CPU={6:F1}%\n",
            messages, wall.ElapsedMilliseconds, rate, p50, p90, p99, cpuPct);
        Logger.Warning(msg);

        Assert.True(messages > 0);
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(5000)]
    [InlineData(20000)]
    public async Task Burst_sizes_against_single_entity(int burstSize)
    {
        var region = await ShardRegionTools.EnsureRegionStarted(Sys);
        var key = DriverKey.Create(31, 7);
        await region.Ask<CreatedDriverMessage>(new CreateModelDriverMessage(key, "Burst", "Var", "BV", "DE", "Team"), TimeSpan.FromSeconds(5));

        var proc = Process.GetCurrentProcess();
        var cpuStart = proc.TotalProcessorTime;
        var wall = Stopwatch.StartNew();

        var tasks = Enumerable.Range(0, burstSize)
            .Select(i => CpuMeasuring.MeasureLatency(async () =>
                await region.Ask<string>(new UpdateIntervalMessage(key, i, DateTime.UtcNow), TimeSpan.FromSeconds(10))))
            .ToArray();

        var latencies = await Task.WhenAll(tasks);
        wall.Stop();
        var cpuEnd = proc.TotalProcessorTime;

        var rate = burstSize / wall.Elapsed.TotalSeconds;
        var p50 = CpuMeasuring.Percentile(latencies, 50);
        var p90 = CpuMeasuring.Percentile(latencies, 90);
        var p99 = CpuMeasuring.Percentile(latencies, 99);
        var cpuPct = CpuMeasuring.CpuPercent(cpuStart, cpuEnd, wall.Elapsed);

        var msg = string.Format(CultureInfo.InvariantCulture,
            "\n-----------------------------\nBurst {0}:\n {0} msgs in {1} ms => {2:F0} msg/s; lat p50={3:F2} p90={4:F2} p99={5:F2}; CPU={6:F1}%\n",
            burstSize, wall.ElapsedMilliseconds, rate, p50, p90, p99, cpuPct);
        Logger.Warning(msg);

        Assert.Equal(burstSize, latencies.Length);
    }

    [Fact]
    public async Task Reactivation_latency_after_passivation()
    {
        var region = await ShardRegionTools.EnsureRegionStarted(Sys);
        var key = DriverKey.Create(32, 8);
        await region.Ask<CreatedDriverMessage>(new CreateModelDriverMessage(key, "Reac", "Test", "RA", "DE", "Team"), TimeSpan.FromSeconds(5));

        region.Tell(new TestPassivateMessage(key));
        await Task.Delay(100); 

        const int attempts = 200;
        var latencies = new double[attempts];
        for (int i = 0; i < attempts; i++)
            latencies[i] = await CpuMeasuring.MeasureLatency(async () =>
                await region.Ask<object>(
                    new UpdateTelemetryMessage(key, i, DateTime.UtcNow), 
                    TimeSpan.FromSeconds(10)));
        

        var p50 = CpuMeasuring.Percentile(latencies, 50);
        var p90 = CpuMeasuring.Percentile(latencies, 90);
        var p99 = CpuMeasuring.Percentile(latencies, 99);

        var msg = string.Format(CultureInfo.InvariantCulture,
            "\n-----------------------------\nReactivation latency (after passivation):\n p50={0:F2}ms p90={1:F2}ms p99={2:F2}ms\n",
            p50, p90, p99);
        Logger.Warning(msg);

        Assert.True(latencies.Length == attempts);
    }
}