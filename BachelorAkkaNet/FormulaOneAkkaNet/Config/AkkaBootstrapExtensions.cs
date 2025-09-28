using Akka.Cluster.Hosting;
using Akka.Cluster.Sharding;
using Akka.Hosting;
using Akka.Persistence.Hosting;
using FormulaOneAkkaNet.Coordinator;
using FormulaOneAkkaNet.Coordinator.Listeners;
using FormulaOneAkkaNet.Ingress;
using FormulaOneAkkaNet.ShardRegion;
using FormulaOneAkkaNet.ShardRegion.Utilities;
using Infrastructure;
using Infrastructure.General;

namespace FormulaOneAkkaNet.Config;

internal static class AkkaBootstrapExtensions
{
    public static IServiceCollection UseAkka(this IServiceCollection sp, AkkaConfig akkaHc, ConfigurationManager manager)
    {
        string connectionString = manager.GetConnectionString("PostgreSql") ?? string.Empty;
        sp.AddAkka(akkaHc.ClusterName, (akka, sp) =>
        {
            
            akka.UseAkkaLogger();
            akka.UseRemoteCluster(akkaHc);

            if (akkaHc.Roles.Contains(ClusterMemberRoles.Controller.ToStr()))
                akka.RegisterCoordinator(akkaHc);

            if (akkaHc.Roles.Contains(ClusterMemberRoles.Backend.ToStr()))
                akka.RegisterShardRegion(akkaHc);

            if (akkaHc.Roles.Contains(ClusterMemberRoles.Ingress.ToStr()))
                akka.RegisterIngress(akkaHc);

            if (!akkaHc.Roles.Contains(ClusterMemberRoles.Controller.ToStr()))
                akka.RegisterControllerProxy();
            // if (akkaHc.Roles.Contains(ClusterMemberRoles.Api.ToStr()))
        });
        return sp;
    }

    private static void RegisterCoordinator(this AkkaConfigurationBuilder config, AkkaConfig akkaHc)
    {
        string role = ClusterMemberRoles.Controller.ToStr();

        config.WithSingleton<ClusterCoordinatorMarker>(
                singletonName: role, // akkaHc.Role,
                propsFactory: (_, _, resolver) => resolver.Props<ClusterCoordinator>(),
                options: new ClusterSingletonOptions { Role = akkaHc.Role })
            .WithActors((system, registry, resolver) =>
            {
                var controller = registry.Get<ClusterCoordinatorMarker>();
                // Restart after exception throws
                // else supervisor strategy is needed
                registry.Register<ClusterEventListener>(
                    system.ActorOf(resolver.Props<ClusterEventListener>(),
                        "cluster-event-listener"));

                registry.Register<IngressListener>(
                    system.ActorOf(resolver.Props<IngressListener>(controller),
                        "ingress-listener"));

                registry.Register<ShardListener>(
                    system.ActorOf(resolver.Props<ShardListener>(controller),
                        "shard-listener"));
            });
    }

    private static void RegisterShardRegion(this AkkaConfigurationBuilder config, AkkaConfig akkaHc, IMessageExtractor? ex = null)
    {
        string role = ClusterMemberRoles.Backend.ToStr();

        var extractor = ex ?? new DriverMessageExtractor();

        config.WithShardingDistributedData(options =>
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
                // optional: enable durable storage of DData to survive node restarts
                options.Durable.Keys = [];
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
                    Role = role, // akkaHc.Role,
                    // passivate idle entities after 5 minute
                    PassivateIdleEntityAfter = TimeSpan.FromMinutes(5),
                    // Use DData for state store needed for sharding persistence
                    // use DData for state store
                    StateStoreMode = StateStoreMode.DData,
                    // to remember entities in the cluster and rebalanced them
                    RememberEntities = true,
                    // optional: use DData for state store
                    //RememberEntitiesStore = RememberEntitiesStore.DData
                })
            .WithActors((system, registry, resolver) =>
                registry.Register<TelemetryRegionHandler>(
                    system.ActorOf(resolver.Props<TelemetryRegionHandler>(),
                        "telemetry-region-handler")));
    }

    private static void RegisterBackendJournal(this AkkaConfigurationBuilder config, string connectionString)
    {
        string dbName = "driverRegion";
#if DEBUG
        config.WithInMemoryJournal(journalId: dbName, journalBuilder: _ => {});
        config.WithInMemorySnapshotStore(dbName);
#else
        var dataSource = new NpgsqlDataSourceBuilder(connectionString).Build();

        var dataOptions = new DataOptions()
            .UseDataProvider(
                DataConnection.GetDataProvider(
                    ProviderName.PostgreSQL, 
                    dataSource.ConnectionString) ?? throw new Exception("Could not get data provider"))
            .UseProvider(ProviderName.PostgreSQL)
            .UseConnectionFactory((opt) => dataSource.CreateConnection());
        
        config.WithSqlPersistence(dataOptions, autoInitialize: true, schemaName: "public");
#endif
    }

    private static void RegisterIngress(this AkkaConfigurationBuilder config, AkkaConfig akkaHc)
    {
        config.WithShardRegionProxy<DriverRegionProxyMarker>(
                typeName: akkaHc.ShardName,
                roleName: ClusterMemberRoles.Backend.ToStr(), // null!, <-- when all nodes can host the shard region
                messageExtractor: new DriverMessageExtractor())
            .WithActors((system, registry, resolver) =>
            {
                // actor for handling shard region proxy messages
                // not use only testing
                // registry.Register<ShardRegionProxy>(system.ActorOf(resolver.Props<ShardRegionProxy>(), "proxy"));

                registry.Register<IngressControllerActor>(
                    system.ActorOf(resolver.Props<IngressControllerActor>(),
                        "controller-handler"));
            });
    }

    private static void RegisterControllerProxy(this AkkaConfigurationBuilder config)
    {
        config.WithSingletonProxy<ClusterCoordinatorMarker>(
            singletonName: ClusterMemberRoles.Controller.ToStr(),
            singletonManagerName: ClusterMemberRoles.Controller.ToStr());
    }
}