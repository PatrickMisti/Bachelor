using Akka.Cluster.Hosting;
using Akka.Hosting;
using Akka.Persistence.Sql.Hosting;
using ClusterCoordinator.Actors;
using ClusterCoordinator.Actors.Listeners;
using Infrastructure.General;
using Infrastructure.Shard;

namespace ClusterCoordinator.Config;

public static class AkkaBootstrapExtension
{
    public static IServiceCollection ConfigureCoordinator(this IServiceCollection sp, AkkaHostingConfig akkaHc,
        string sqlitePath)
    {
        // is needed bc the sqlite path can be relative
        //Directory.CreateDirectory(Path.GetDirectoryName(sqlitePath)!);

        sp.AddAkka(akkaHc.ClusterName, config =>
        {
            config
                .UseRemoteCluster(akkaHc)
                .UseAkkaLogger()
                /*.WithSqlPersistence(jb =>
                    {
                        jb.ProviderName = "Sqlite";
                        jb.ConnectionString = $"Data Source={sqlitePath}";
                        jb.AutoInitialize = true;
                        //jb.Identifier = "akka.persistence.journal.sqlite";
                    },
                    snap =>
                    {
                        snap.ProviderName = "Sqlite";
                        snap.ConnectionString = $"Data Source={sqlitePath}";
                        snap.AutoInitialize = true;
                        //snap.Identifier = "akka.persistence.snapshot-store.sqlite";
                    })*/
                .WithSingleton<ClusterCoordinatorMarker>(
                    singletonName: akkaHc.Role,
                    propsFactory: (_, _, resolver) => resolver.Props<ClusterController>(),
                    options: new ClusterSingletonOptions { Role = akkaHc.Role }
                )
                .WithActors((system, registry, resolver) =>
                {
                    var controller = registry.Get<ClusterCoordinatorMarker>();
                    // Restart after exception throws
                    // else supervisor strategy is needed
                    registry.Register<ClusterEventListener>(
                        system.ActorOf(resolver.Props<ClusterEventListener>(), "cluster-event-listener"));

                    registry.Register<IngressListener>(
                        system.ActorOf(resolver.Props<IngressListener>(controller), "ingress-listener"));

                    registry.Register<ShardListener>(
                        system.ActorOf(resolver.Props<ShardListener>(controller), "shard-listener"));
                });
        });

        return sp;
    }
}