using Infrastructure.Cluster.Config;
using Akka.Cluster.Hosting;
using Akka.Hosting;
using DiverShardHost.Cluster.Actors;
using Infrastructure.Cluster.Base;
using Infrastructure.General;

namespace DiverShardHost.Config;

public static class AkkaBootstrapExtension
{
    public static IServiceCollection ConfigureShardRegion(this IServiceCollection sp, AkkaHostingConfig akkaHc)
    {

        sp.AddAkka(akkaHc.ClusterName, config =>
        {
            config
                .UseRemoteCluster(akkaHc)
                .UseAkkaLogger()
                .WithShardRegion<DriverData>(
                    typeName: akkaHc.ShardName,
                    entityPropsFactory: (_, _, resolver) => _ => resolver.Props<DriverActor>(),
                    messageExtractor: new DriverMessageExtractor(),
                    shardOptions: new ShardOptions
                    {
                        Role = akkaHc.Role,
                        PassivateIdleEntityAfter = TimeSpan.FromMinutes(1)
                    });
        });

        return sp;
    }
}