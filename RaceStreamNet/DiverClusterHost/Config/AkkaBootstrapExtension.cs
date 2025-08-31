using Akka.Cluster.Hosting;
using Akka.Cluster.Sharding;
using Akka.Hosting;
using DriverShardHost.Actors;
using Infrastructure.General;
using Infrastructure.Shard;

namespace DriverShardHost.Config;

public static class AkkaBootstrapExtension
{
    public static IServiceCollection ConfigureShardRegion(this IServiceCollection sp, AkkaHostingConfig akkaHc, IMessageExtractor extractor)
    {

        sp.AddAkka(akkaHc.ClusterName, config =>
        {
            config
                .UseRemoteCluster(akkaHc)
                .UseAkkaLogger()
                .WithShardingDistributedData(options =>
                {
                    options.RecreateOnFailure = true;
                    // MajorityMinimumCapacity defines the minimum number of cluster nodes
                    // required to form a "majority" quorum for DistributedData operations.
                    // - Ensures data is not considered confirmed with too few nodes.
                    // - Useful during cluster startup to avoid single-node "majorities".
                    // - For small clusters (2–3 nodes), set to 2.
                    // - For larger clusters, prefer 3–5 depending on expected size.
                    options.MajorityMinimumCapacity = 1;
                    // MaxDeltaElements controls the size of gossip deltas in Akka.DistributedData.
                    // - DData propagates changes incrementally (delta-state).
                    // - Small values  => many small gossip messages (higher overhead, more traffic).
                    // - Large values  => fewer gossip messages, but larger payloads.
                    // - Default is ~1000, practical range is 500–5000.
                    // Warning: very small values (e.g. 3) can cause excessive gossip traffic!
                    options.MaxDeltaElements = 2000;
                    // optional: DData-Durability (local LMDB-Puffer)
                    // options.Durable.Keys = new[] { "sharding.*" };
                    // options.Durable.Lmdb.Directory = "/var/lib/akka/ddata";
                    // options.Durable.Lmdb.MapSize = 512L * 1024 * 1024; 
                })
                .WithShardRegion<DriverRegionMarker>(
                    typeName: akkaHc.ShardName,
                    entityPropsFactory: (_, _, resolver) => _ => resolver.Props<DriverActor>(),
                    messageExtractor: extractor,
                    shardOptions: new ShardOptions
                    {
                        Role = akkaHc.Role,
                        // passivate idle entities after 5 minute
                        PassivateIdleEntityAfter = TimeSpan.FromMinutes(5),
                        // Use DData for state store needed for sharding persistence
                        // use DData for state store
                        StateStoreMode = StateStoreMode.DData,
                        // to remember entities in the cluster and rebalanced them
                        RememberEntities = true,
                        // optional: use DData for state store
                        RememberEntitiesStore = RememberEntitiesStore.DData
                        
                    })
                .WithActors((system, registry, resolver) => 
                {
                    var notifierProps = resolver.Props<TelemetryRegionHandler>();
                    var notifierActor = system.ActorOf(notifierProps, "telemetry-region-handler");
                    registry.Register<TelemetryRegionHandler>(notifierActor);
                });
        });

        return sp;
    }

    public static IServiceCollection ConfigureShardRegion(this IServiceCollection sp, AkkaHostingConfig akkaHc, int? maxShardCount = null)
        => ConfigureShardRegion(
            sp, 
            akkaHc, 
            maxShardCount is null 
                ? new DriverMessageExtractor() 
                : new DriverMessageExtractor((int) maxShardCount));

}