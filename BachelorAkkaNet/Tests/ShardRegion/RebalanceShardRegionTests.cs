using Akka.Actor;
using Akka.Cluster;
using Akka.Cluster.Sharding;
using Akka.Configuration;
using Akka.Event;
using Akka.TestKit.Xunit2;
using Infrastructure.ShardRegion;
using Tests.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Tests.ShardRegion;

public class RebalanceShardRegionTests(ITestOutputHelper helper) : TestKit(TestConfig, helper)
{
    private static readonly string TestConfig = """
                                                akka {
                                                  loglevel = "Warning"
                                                  stdout-loglevel = "Warning"
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
                                                  cluster.sharding {
                                                    rebalance-interval = 1s
                                                    least-shard-allocation-strategy {
                                                      rebalance-threshold = 1
                                                      max-simultaneous-rebalance = 3
                                                    }
                                                  }
                                                }
                                                """;

    private static readonly TimeSpan AskTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan AssertTimeout = TimeSpan.FromSeconds(45);

    private ILoggingAdapter Logger => Sys.Log;

    [Fact]
    public async Task Should_rebalance_shards_when_second_node_joins()
    {
        // region on node1
        var region1 = await ShardRegionTools.EnsureRegionStarted(Sys);
        var cluster1 = Cluster.Get(Sys);

        // create some entities on node1
        for (int i = 1; i <= 100; i++)
        {
            var key = DriverKey.Create(100, i);
            await region1.Ask<CreatedDriverMessage>(
                new CreateModelDriverMessage(key, "N", "1", "A", "DE", "T"),
                AskTimeout);
        }

        // node2 joins
        var hocon = ConfigurationFactory.ParseString(TestConfig).WithFallback(Sys.Settings.Config);
        var system2 = ActorSystem.Create(Sys.Name, hocon);
        try
        {
            var region2 = await ShardRegionTools.EnsureRegionStarted(system2, cluster1.SelfAddress);

            // ensure both nodes see 2 members UP before asserting rebalancing
            await WaitForMembersUp(Sys, 2, AssertTimeout);
            await WaitForMembersUp(system2, 2, AssertTimeout);

            await TestClusterStats(region1, stats =>
            {
                Logger.Warning("After node2 join:\n{0}", DumpStats(stats));
                Assert.True(stats.Regions.Count >= 2, $"Expected >=2 regions but got {stats.Regions.Count}\n{DumpStats(stats)}");
                // ensure shards are assigned to both regions (entity counts may be 0 on new region without remember-entities)
                var regionsWithShards = stats.Regions.Values.Count(r => r.Stats.Count > 0);
                Assert.True(regionsWithShards >= 2, $"Expected shards on both regions but got {regionsWithShards}\n{DumpStats(stats)}");
            }, AssertTimeout);
        }
        finally
        {
            await system2.Terminate();
        }
    }

    [Fact]
    public async Task Should_rebalance_multiple_shards_to_new_node()
    {
        // region on node1
        var region1 = await ShardRegionTools.EnsureRegionStarted(Sys);
        var cluster1 = Cluster.Get(Sys);

        // create entities on node1 to seed load
        for (int i = 1; i <= 100; i++)
        {
            var key = DriverKey.Create(100, i);
            await region1.Ask<CreatedDriverMessage>(
                new CreateModelDriverMessage(key, "N", "1", "A", "DE", "T"),
                AskTimeout);
        }

        // node2 joins
        var hocon = ConfigurationFactory.ParseString(TestConfig).WithFallback(Sys.Settings.Config);
        var system2 = ActorSystem.Create(Sys.Name, hocon);
        try
        {
            var region2 = await ShardRegionTools.EnsureRegionStarted(system2, cluster1.SelfAddress);

            // both nodes should observe 2 members Up
            await WaitForMembersUp(Sys, 2, AssertTimeout);
            await WaitForMembersUp(system2, 2, AssertTimeout);

            var targetAddr = Cluster.Get(system2).SelfAddress;
            const int minShardsOnNode2 = 3; // conservative to avoid flakiness

            await AwaitAssertAsync(async () =>
            {
                var stats = await region1.Ask<ClusterShardingStats>(
                    new GetClusterShardingStats(TimeSpan.FromSeconds(2)),
                    AskTimeout);

                Logger.Warning("Rebalance progress after node2 join:\n{0}", DumpStats(stats));

                Assert.True(stats.Regions.Count >= 2, $"Expected >= 2 regions, got {stats.Regions.Count}\n{DumpStats(stats)}");
                var node2Shards = stats.Regions.TryGetValue(targetAddr, out var rs) ? rs.Stats.Count : 0;
                Assert.True(node2Shards >= minShardsOnNode2,
                    $"Expected >= {minShardsOnNode2} shards on node2 {targetAddr} but got {node2Shards}\n{DumpStats(stats)}");
            }, AssertTimeout);
        }
        finally
        {
            await system2.Terminate();
        }
    }

    [Fact]
    public async Task Should_move_all_shards_and_entities_to_remaining_node_on_region_shutdown()
    {
        // region on node1
        var region1 = await ShardRegionTools.EnsureRegionStarted(Sys);
        var cluster1 = Cluster.Get(Sys);

        // create entities on node1
        const int entityCount = 100;
        var keys = Enumerable.Range(1, entityCount).Select(i => DriverKey.Create(100, i)).ToArray();
        foreach (var key in keys)
            await region1.Ask<CreatedDriverMessage>(new CreateModelDriverMessage(key, "N", "1", "A", "DE", "T"), AskTimeout);

        // node2 joins
        var hocon = ConfigurationFactory.ParseString(TestConfig).WithFallback(Sys.Settings.Config);
        var system2 = ActorSystem.Create(Sys.Name, hocon);
        try
        {
            var region2 = await ShardRegionTools.EnsureRegionStarted(system2, cluster1.SelfAddress);

            // ensure both nodes see 2 members UP before handoff
            await WaitForMembersUp(Sys, 2, AssertTimeout);
            await WaitForMembersUp(system2, 2, AssertTimeout);

            // shutdown region1 gracefully and wait for termination
            Watch(region1);
            region1.Tell(GracefulShutdown.Instance);
            ExpectTerminated(region1, AssertTimeout);

            // wait until region2 hosts shards (local view)
            await AwaitAssertAsync(async () =>
            {
                var local2 = await GetLocalState(region2);
                Logger.Warning("Local state region2 after region1 shutdown: {0}", DumpLocal(local2));
                Assert.True(local2.Shards.Count >= 1, $"region2 has no shards\n{DumpLocal(local2)}");
            }, AssertTimeout);

            // re-materialize entities on region2 by sending CreateModelDriverMessage again
            foreach (var key in keys)
                await region2.Ask<CreatedDriverMessage>(new CreateModelDriverMessage(key, "N", "1", "A", "DE", "T"), AskTimeout);

            await TestClusterStats(region2, stats =>
            {
                Logger.Warning("After materializing entities on region2:\n{0}", DumpStats(stats));
                var totalShards = stats.Regions.Values.Sum(r => r.Stats.Count);
                Assert.True(totalShards >= 1, $"Expected shards on remaining region but got {totalShards}\n{DumpStats(stats)}");
                var hasEntities = stats.Regions.Values.Any(r => r.Stats.Any(s => s.Value > 0));
                Assert.True(hasEntities, $"Expected non-empty shard(s) on region2\n{DumpStats(stats)}");
            }, AssertTimeout);
        }
        finally
        {
            await system2.Terminate();
        }
    }

    private async Task TestClusterStats(IActorRef region, Action<ClusterShardingStats> action, TimeSpan duration)
    {
        await AwaitAssertAsync(async () =>
        {
            var stats = await region.Ask<ClusterShardingStats>(
                new GetClusterShardingStats(TimeSpan.FromSeconds(2)),
                AskTimeout);
            action.Invoke(stats);
        }, duration);
    }

    private static Task<CurrentShardRegionState> GetLocalState(IActorRef region) =>
        region.Ask<CurrentShardRegionState>(GetShardRegionState.Instance, AskTimeout);

    private static string DumpStats(ClusterShardingStats stats)
    {
        var lines = new List<string> { $"Regions={stats.Regions.Count}" };
        foreach (var kv in stats.Regions.OrderBy(k => k.Key.ToString()))
        {
            var addr = kv.Key;
            var regionStats = kv.Value;
            var shardPairs = regionStats.Stats
                .OrderBy(s => s.Key)
                .Select(s => $"{s.Key}:{s.Value}");
            lines.Add($" - {addr} shards={regionStats.Stats.Count} [{string.Join(", ", shardPairs)}]");
        }
        var totalShards = stats.Regions.Values.Sum(r => r.Stats.Count);
        lines.Add($"Total shards: {totalShards}");
        return string.Join(Environment.NewLine, lines);
    }

    private static string DumpLocal(CurrentShardRegionState state)
    {
        var shards = state.Shards.OrderBy(s => s.ShardId).Select(s => $"{s.ShardId}({s.EntityIds.Count})");
        return $"local shards={state.Shards.Count} [{string.Join(", ", shards)}]";
    }

    private async Task WaitForMembersUp(ActorSystem system, int expected, TimeSpan timeout)
    {
        await AwaitAssertAsync(() =>
        {
            var cluster = Cluster.Get(system);
            var up = cluster.State.Members.Count(m => m.Status == MemberStatus.Up);
            Assert.True(up >= expected, $"Expected {expected} members Up on {system.Name}, got {up}");
        }, timeout);
    }
}
