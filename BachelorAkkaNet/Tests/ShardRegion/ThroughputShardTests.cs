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
                                                    # more Shards -> better distribution over 2 nodes
                                                    number-of-shards = 200

                                                    # fast rebalance for test
                                                    rebalance-interval = 30s # konservativy 5s up
                                                    least-shard-allocation-strategy {
                                                      rebalance-threshold = 5 # 5 and up is konservativy 
                                                      max-simultaneous-rebalance = 1 # smaller konservativy 5 is heavy
                                                    }
                                                    #to test extreme sharding
                                                    #state-store-mode = ddata
                                                    remember-entities = off # on is more overhead
                                                  }
                                                  cluster.distributed-data {
                                                    majority-min-capacity = 1 #cut overhead for test
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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Throughput_should_increase_when_adding_region(bool withRoundtrip)
    {
        const int entities = 200;
        const int messagesPerEntity = 200;
        const int totalMessages = entities * messagesPerEntity;

        ActorSystem? system2 = null;
        try
        {
            var region1 = await ShardRegionTools.EnsureRegionStarted(Sys);

            var keys = GenerateKeys(entities);
            await AwaitAndCheckEntitiesVisible(region1, keys);

            // warmup
            await RunMessagingToRegion(region1, keys, 10);

            // in milliseconds
            var latency1 = withRoundtrip 
                ? await RunMessagingToRegion(region1, keys, messagesPerEntity) 
                : await RunMessagingToRegionTell(region1,keys, messagesPerEntity);

            // start 2nd region in new system
            system2 = NewSys();
            var region2 = await ShardRegionTools.EnsureRegionStarted(system2, Cluster.Get(Sys).SelfAddress);
            await AwaitAndCheckClusterVisible(region1, region2, keys);

            var newKeys = GenerateKeys(entities * 2).Skip(entities).ToArray();
            // Create entities via region2, but validate visibility across the cluster
            await CreateDriverInRegion(region2, newKeys);
            await AwaitEntitiesClusterVisible(region1, region2, newKeys, TimeSpan.FromSeconds(20));

            // warmup 2nd region
            await RunMessagingToRegion(region2, newKeys, 10);
            

            var keysFirst = newKeys.Take(keys.Length / 2).ToArray();
            var keysSecond = newKeys.Skip(keys.Length / 2).ToArray();

            var latency2 = withRoundtrip
                ? await CpuMeasuring.MeasureLatency(() => Task.WhenAll(
                    SendWorkload(region1, keysFirst, messagesPerEntity),
                    SendWorkload(region2, keysSecond, messagesPerEntity)
                ))
                : await CpuMeasuring.MeasureLatency(() => Task.WhenAll(
                    SendWorkloadTell(region1, keysFirst, messagesPerEntity),
                    SendWorkloadTell(region2, keysSecond, messagesPerEntity)
                ));

            var rate1 = totalMessages / (latency1 / 1000.0);
            var rate2 = totalMessages / (latency2 / 1000.0);

            var msg = string.Format(CultureInfo.InvariantCulture,
                "\n-----------------------------\nThroughput with 1 region: {0:F0} msg/s\n" +
                "Throughput with 2 regions: {1:F0} msg/s\n",
                rate1, rate2);

            Logger.Warning(msg);

            Assert.True(rate2 / rate1 > 1.4, $"gain={rate2/rate1:F} expected ≥40% gain with 2 nodes, got {rate1:F0}→{rate2:F0} msg/s");
        }
        finally
        {
            await system2?.Terminate()!;
        }
    }
}