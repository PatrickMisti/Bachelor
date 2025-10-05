using Akka.Actor;
using Akka.Cluster;
using Akka.Cluster.Sharding;
using Tests.ShardRegion.Assets;

namespace Tests.Utilities;

internal static class ShardRegionTools
{
    public static async Task<IActorRef> EnsureRegionStarted(ActorSystem sys, Address? joinTo = null)
    {
        // form 1-node cluster
        var cluster = Cluster.Get(sys);
        var target = joinTo ?? cluster.SelfAddress;
        await cluster.JoinAsync(target);
        await WaitForMemberUpAsync(cluster, TimeSpan.FromSeconds(15));

        var sharding = ClusterSharding.Get(sys);
        const string typeName = "driver-test";

        var settings = ClusterShardingSettings.Create(sys);
        var extractor = new TestDriverMessageExtractor(10);

        var region = await sharding.StartAsync(
            typeName: typeName,
            entityProps: DriverTestActor.Prop(),
            settings: settings,
            messageExtractor: extractor);

        return region;
    }

    private static async Task WaitForMemberUpAsync(Cluster cluster, TimeSpan timeout)
    {
        if (cluster.SelfMember.Status == MemberStatus.Up)
            return;

        var tcs = new TaskCompletionSource();
        void OnUp() => tcs.TrySetResult();

        cluster.RegisterOnMemberUp(OnUp);
        try
        {
            using var cts = new CancellationTokenSource(timeout);
            await tcs.Task.WaitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException("Cluster member did not reach Up state in time.");
        }
    }
}