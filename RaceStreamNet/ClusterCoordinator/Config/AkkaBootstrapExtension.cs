using Akka.Cluster.Hosting;
using Akka.Hosting;
using Akka.Persistence.Sql.Hosting;
using ClusterCoordinator.Listeners;
using Infrastructure.Cluster.Config;
using Infrastructure.General;

namespace ClusterCoordinator.Config;

public static class AkkaBootstrapExtension
{
    private static readonly string ShardMonitorControllerName = "shard-monitor";
    public static IServiceCollection ConfigureCoordinator(this IServiceCollection sp, AkkaHostingConfig akkaHc,
        string sqlitePath)
    {
        // is needed bc the sqlite path can be relative
        Directory.CreateDirectory(Path.GetDirectoryName(sqlitePath)!);

        sp.AddAkka(akkaHc.ClusterName, config =>
        {
            config
                .UseRemoteCluster(akkaHc)
                .UseAkkaLogger()
                .WithSqlPersistence(jb =>
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
                    })
                .WithSingleton<ClusterController>(
                    singletonName: akkaHc.Role,
                    propsFactory: (_, _, resolver) => resolver.Props<ClusterController>(),
                    options: new ClusterSingletonOptions { Role = akkaHc.Role }
                ).WithActors((system, registry, resolver) =>
                {
                    var shardMonitorProps = resolver.Props<ShardListener>();
                    var shardMonitorActor = system.ActorOf(shardMonitorProps, ShardMonitorControllerName);

                    registry.Register<ShardListener>(shardMonitorActor);
                });
        });

        return sp;
    }
}