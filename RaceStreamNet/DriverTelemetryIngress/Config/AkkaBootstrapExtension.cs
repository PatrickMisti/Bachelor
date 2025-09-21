using Akka.Cluster.Hosting;
using Akka.Hosting;
using DriverTelemetryIngress.Actors;
using DriverTelemetryIngress.Bridge;
using Infrastructure.General;
using Infrastructure.Shard;

namespace DriverTelemetryIngress.Config;

public static class AkkaBootstrapExtension
{
    private static readonly string DefaultUrl = "https://api.openf1.org";

    public static IServiceCollection ConfigureHttp(this IServiceCollection sp, string? url = null)
    {
        sp.AddHttpClient<IHttpWrapperClient, OpenF1Client>(client =>
        {
            // Set the base address for the HttpClient
            client.BaseAddress = new Uri(url ?? DefaultUrl);
            // Set a reasonable timeout for requests
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        return sp;
    }

    public static IServiceCollection ConfigureStreams(this IServiceCollection sp, AkkaHostingConfig akkaHc)
    {
        sp.AddAkka(akkaHc.ClusterName, config =>
        {
            config
                .UseRemoteCluster(akkaHc)
                .UseAkkaLogger()
                .WithShardRegionProxy<DriverRegionMarker>(
                    typeName: akkaHc.ShardName,
                    roleName: null!,
                    messageExtractor: new DriverMessageExtractor())
                .WithSingletonProxy<ClusterCoordinatorMarker>(
                    singletonName: ClusterMemberEnum.Controller.ToStr(),
                    singletonManagerName: ClusterMemberEnum.Controller.ToStr())
                .WithActors((system, registry, resolver) =>
                {
                    // actor for handling shard region proxy messages
                    // not use only testing
                    // registry.Register<ShardRegionProxy>(system.ActorOf(resolver.Props<ShardRegionProxy>(), "proxy"));

                    registry.Register<ControllerHandlerActor>(
                        system.ActorOf(resolver.Props<ControllerHandlerActor>(), "controller-handler"));
                });
        });

        // Not needed with PreStart Context.System.Materializer() in actors
        // Register Materializer
        /*sp.AddSingleton<IMaterializer>(ctx =>
        {
            var mat = ctx.GetRequiredService<ActorSystem>();
            return mat.Materializer();
        });*/

        return sp;
    }
}