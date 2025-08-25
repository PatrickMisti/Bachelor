using Akka.Actor;
using Akka.Hosting;
using Akka.Streams;
using Infrastructure.General;

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
                .WithActors((system, registry, resolver) =>
                {

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