using Akka.Actor;
using Akka.Cluster;
using Akka.Cluster.Sharding;
using Akka.Configuration;
using Akka.Event;
using Akka.TestKit.Xunit2;
using Akka.Util.Internal;
using Infrastructure.ShardRegion;
using System.Drawing;
using System.Globalization;
using Tests.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Tests.ShardRegion;

public class ThroughputShardTests(ITestOutputHelper helper) : TestKit(TestConfig, helper)
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
                                                
                                                  cluster {
                                                    min-nr-of-members = 1
                                                    roles = ["test"]
                                                
                                                    distributed-data {
                                                      majority-min-capacity = 2
                                                      max-delta-elements = 2000
                                                    }
                                                  }
                                                  cluster.sharding {
                                                    number-of-shards = 200
                                                    rebalance-interval = 1s # aggressive for test
                                                    least-shard-allocation-strategy {
                                                      rebalance-threshold = 1
                                                      max-simultaneous-rebalance = 20
                                                    }
                                                    remember-entities = off
                                                  }
                                                  cluster.distributed-data {
                                                    majority-min-capacity = 1
                                                  }
                                                  persistence {
                                                    journal.plugin = "akka.persistence.journal.inmem"
                                                    snapshot-store.plugin = "akka.persistence.snapshot-store.inmem"
                                                  }
                                                
                                                  loggers = [
                                                    "Akka.TestKit.TestEventListener, Akka.TestKit",
                                                    "Akka.Event.StandardOutLogger, Akka"
                                                  ]
                                                }
                                                """;

    private ILoggingAdapter Logger => Sys.Log;

    private async Task SendWorkload(IActorRef region, DriverKey[] keys, int mpe)
    {
        var tasks = keys.SelectMany(k =>
                Enumerable.Range(0, mpe).Select(i =>
                    region.Ask<string>(new UpdateTelemetryMessage(k, i, DateTime.UtcNow),
                        TimeSpan.FromSeconds(10))))
            .ToArray();

        await Task.WhenAll(tasks);
    }

    private async Task SendWorkloadTell(IActorRef region, DriverKey[] keys, int mpe)
    {
        keys.ForEach(k =>
            Enumerable.Range(0, mpe).ForEach(i =>
                region.Tell(new UpdateTelemetryMessage(k, i, DateTime.UtcNow))));

        await Task.CompletedTask;
    }

    private static async Task SendWorkloadSave(IActorRef region, DriverKey[] keys, int mpe)
    {
        var tasks = keys.SelectMany(k =>
                Enumerable.Range(0, mpe).Select(async i =>
                {
                    async Task SendOnce()
                    {
                        var res = await region.Ask<object>(
                            new UpdateTelemetryMessage(k, i, DateTime.UtcNow),
                            TimeSpan.FromSeconds(15));

                        if (res is NotInitializedMessage)
                        {
                            await Task.Delay(25);
                            res = await region.Ask<object>(
                                new UpdateTelemetryMessage(k, i, DateTime.UtcNow),
                                TimeSpan.FromSeconds(10));
                        }
                    }

                    await SendOnce();
                }))
            .ToArray();

        await Task.WhenAll(tasks);
    }

    private async Task<double> RunMessagingToRegion(IActorRef region, DriverKey[] keys, int mpe) => 
        await CpuMeasuring.MeasureLatency(() => SendWorkload(region, keys, mpe));

    private async Task<double> RunMessagingToRegionTell(IActorRef region, DriverKey[] keys, int mpe) =>
        await CpuMeasuring.MeasureLatency(() => SendWorkloadTell(region, keys, mpe));

    private DriverKey[] GenerateKeys(int count) =>
        Enumerable.Range(1, count).Select(i => DriverKey.Create(100, i)).ToArray();

    private ActorSystem NewSys() =>
        ActorSystem.Create(Sys.Name, ConfigurationFactory.ParseString(TestConfig).WithFallback(Sys.Settings.Config));

    private async Task CreateDriverInRegion(IActorRef region, DriverKey[] keys)
    {
        foreach (var k in keys)
        {
            var s = await region.Ask<CreatedDriverMessage>(new CreateModelDriverMessage(k, "A", "B", "C", "D", "E"));
            Assert.True(s.IsSuccess, $"failed to create {k}: {s.ErrorMsg}");
        }
    }

    private async Task AwaitEntitiesVisible(IActorRef region, IEnumerable<DriverKey> keys, TimeSpan timeout)
    {
        var expected = keys.Select(k => k.ToString()).ToHashSet();
        await AwaitAssertAsync(async () =>
        {
            var state = await region.Ask<CurrentShardRegionState>(GetShardRegionState.Instance, TimeSpan.FromSeconds(5));
            var all = state.Shards.SelectMany(s => s.EntityIds);
            Assert.True(expected.IsSubsetOf(all), "Not all entities are materialized yet");
        }, timeout);
    }

    private async Task AwaitEntitiesClusterVisible(
        IActorRef region1, IActorRef region2, IEnumerable<DriverKey> keys, TimeSpan timeout)
    {
        var expected = keys.Select(k => k.ToString()).ToHashSet();
        await AwaitAssertAsync(async () =>
        {
            var s1 = await region1.Ask<CurrentShardRegionState>(GetShardRegionState.Instance, TimeSpan.FromSeconds(5));
            var s2 = await region2.Ask<CurrentShardRegionState>(GetShardRegionState.Instance, TimeSpan.FromSeconds(5));
            var all = s1.Shards.SelectMany(s => s.EntityIds).Concat(s2.Shards.SelectMany(s => s.EntityIds)).ToHashSet();
            Assert.True(expected.IsSubsetOf(all), "Not all entities are materialized across the cluster yet");
        }, timeout);
    }

    private async Task AwaitShardDistributed(IActorRef region, int regions)
    {
        await AwaitAssertAsync(async () =>
        {
            var stats = await region.Ask<ClusterShardingStats>(
                new GetClusterShardingStats(TimeSpan.FromSeconds(2)), TimeSpan.FromSeconds(10));
            Assert.True(stats.Regions.Count >= regions);
            Assert.True(stats.Regions.Values.Sum(r => r.Stats.Count) >= 1);
        }, TimeSpan.FromSeconds(30));
    }

    private async Task AwaitAndCheckEntitiesVisible(IActorRef region, DriverKey[] keys)
    {
        await CreateDriverInRegion(region, keys);
        await AwaitEntitiesVisible(region, keys, TimeSpan.FromSeconds(20));
    }

    private async Task AwaitAndCheckClusterVisible(IActorRef oldRegion, IActorRef newRegion, DriverKey[] keys)
    {
        await AwaitShardDistributed(newRegion, 2);
        await AwaitEntitiesClusterVisible(oldRegion, newRegion, keys, TimeSpan.FromSeconds(20));
    }

    private async Task AwaitShardsBalanced(IActorRef region, int expectedRegions, int allowedDiff = 10)
    {
        await AwaitAssertAsync(async () =>
        {
            var stats = await region.Ask<ClusterShardingStats>(
                new GetClusterShardingStats(TimeSpan.FromSeconds(2)), TimeSpan.FromSeconds(10));
            Assert.True(stats.Regions.Count == expectedRegions);
            var counts = stats.Regions.Values.Select(r => r.Stats.Count).ToArray();
            Assert.True(counts.Max() - counts.Min() <= allowedDiff, $"Shard distribution not balanced: [{string.Join(",", counts)}]");
        }, TimeSpan.FromSeconds(60));
    }

    private async Task<double> RunWithRuns(IActorRef region, DriverKey[] keys, int mpe, int runs, bool withRoundtrip)
    {
        var latencies = new double[runs];

        for (int i = 0; i < runs; i++)
            latencies[i] = withRoundtrip
                ? await RunMessagingToRegion(region, keys, mpe)
                : await RunMessagingToRegionTell(region, keys, mpe);

        return latencies.OrderBy(x => x).ElementAt(runs / 2);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)] // but flaky should be fixed with Actor who collects messages and acks when done
    public async Task Throughput_should_increase_when_adding_region(bool withRoundtrip)
    {
        const int entities = 200;
        const int messagesPerEntity = 200;
        const int totalMessages = entities * messagesPerEntity;
        const int runs = 3;

        ActorSystem? system2 = null;
        try
        {
            var region1 = await ShardRegionTools.EnsureRegionStarted(Sys);
            var keys = GenerateKeys(entities);
            await AwaitAndCheckEntitiesVisible(region1, keys);
            await RunMessagingToRegion(region1, keys, 10); // warmup

            // Run N times and take median
            var latency1 = await RunWithRuns(region1, keys, messagesPerEntity, runs, withRoundtrip);

            // Start 2nd region
            system2 = NewSys();
            var region2 = await ShardRegionTools.EnsureRegionStarted(system2, Cluster.Get(Sys).SelfAddress);
            await AwaitAndCheckClusterVisible(region1, region2, keys);
            await AwaitShardsBalanced(region1, 2);

            //await RunMessagingToRegion(region1, keys, 10); // warmup again

            var latency2 = await RunWithRuns(region1, keys, messagesPerEntity, runs, withRoundtrip);

            var rate1 = totalMessages / (latency1 / 1000.0);
            var rate2 = totalMessages / (latency2 / 1000.0);

            var msg = string.Format(CultureInfo.InvariantCulture,
                "\n-----------------------------\nThroughput with 1 region: {0:F0} msg/s\n" +
                "Throughput with 2 regions: {1:F0} msg/s\n",
                rate1, rate2);

            Logger.Warning(msg);

            Assert.True(rate2 / rate1 > 1.2, $"gain={rate2/rate1:F} expected ≥20% gain with 2 nodes, got {rate1:F0}→{rate2:F0} msg/s");
        }
        finally
        {
            await system2?.Terminate()!;
        }
    }
}