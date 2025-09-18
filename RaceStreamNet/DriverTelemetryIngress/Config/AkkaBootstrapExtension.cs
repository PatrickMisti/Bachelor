using Akka.Actor;
using Akka.Cluster.Hosting;
using Akka.Hosting;
using Akka.Streams;
using DriverTelemetryIngress.Actors;
using Infrastructure.General;
using Infrastructure.Shard;
using Infrastructure.Shard.Messages;
using Infrastructure.Shard.Models;

namespace DriverTelemetryIngress.Config;

public static class AkkaBootstrapExtension
{
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
                    singletonName: ClusterMemberEnum.Controller.ToStr())
                .WithActors((system, registry, resolver) =>
                {
                    // actor for handling shard region proxy messages
                    var proxyShard = system.ActorOf(resolver.Props<ShardRegionProxy>(), "proxy");
                    registry.Register<ShardRegionProxy>(proxyShard);

                    //proxyShard.Tell((object)new CreateModelDriverMessage(DriverKey.Create(1111, 11)!, "Max", "Verstappen", "VER", "NED", "Red Bull"));
                    registry.Register<ControllerHandlerActor>(system.ActorOf(resolver.Props<ControllerHandlerActor>(), "controller-handler"));

                    proxyShard.Tell((object)new CreateModelDriverMessage(DriverKey.Create(2222, 15)!, "Max", "Verstappen", "VER", "NED", "Red Bull"));
                    proxyShard.Tell(new UpdateTelemetryMessage(DriverKey.Create(2222,15)!, 200, DateTime.Now));
                });
        });

        // Register Materializer
        sp.AddSingleton<IMaterializer>(ctx =>
        {
            var mat = ctx.GetRequiredService<ActorSystem>();
            return mat.Materializer();
        });

        return sp;
    }
}