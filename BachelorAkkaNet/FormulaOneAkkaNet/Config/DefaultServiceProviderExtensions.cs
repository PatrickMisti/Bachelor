using Akka.Cluster.Hosting;
using Akka.Hosting;
using Infrastructure.Http;
using Serilog;
using Akka.Logger.Serilog;
using Akka.Remote.Hosting;
using Akka.Serialization;

namespace FormulaOneAkkaNet.Config;

public static class DefaultServiceProviderExtensions
{
    public static IHostApplicationBuilder CreateLoggingAdapter(this IHostApplicationBuilder builder)
    {
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration)
            .CreateLogger();

        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(Log.Logger);

        return builder;
    }

    public static IServiceCollection AddHttpService(this IServiceCollection sp, string url)
    {
        sp.AddHttpClient<IHttpWrapperClient, HttpOpenF1Client>(client =>
        {
            // Set the base address for the HttpClient
            client.BaseAddress = new Uri(url);
            // Set a reasonable timeout for requests
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        return sp;
    }

    public static AkkaConfigurationBuilder UseRemoteCluster(this AkkaConfigurationBuilder builder,
        AkkaConfig config)
    {
        builder
            .WithRemoting(
                port: config.Port,
                hostname: config.Hostname)
            .WithClustering(
                new ClusterOptions
                {
                    SeedNodes = config.Roles.Count == 1 ? config.SeedNodes : [config.SeedNodes.First()],
                    Roles = config.Roles.ToArray()
                })
            .WithDistributedPubSub(role: null!)
            .AddHocon("""
                          akka.remote.dot-netty.tcp {
                            maximum-frame-size  = 2 MiB
                            send-buffer-size    = 2 MiB
                            receive-buffer-size = 2 MiB
                          }
                          """, HoconAddMode.Append);

        return builder;
    }

    public static AkkaConfigurationBuilder UseAkkaLogger(this AkkaConfigurationBuilder builder)
    {

        builder.ConfigureLoggers(logger =>
        {
            logger.LogLevel = Akka.Event.LogLevel.InfoLevel;
            logger.ClearLoggers();
            // Logging.GetLogger(context.System, "echo")
            // to use the logger in the actor echo is actor
            logger.AddLogger<SerilogLogger>();
        });

        return builder;
    }

    public static AkkaConfigurationBuilder UseHyperion(this AkkaConfigurationBuilder builder)
    {
        builder
            .WithCustomSerializer(
                serializerIdentifier: "hyperion",
                // fallback
                boundTypes: [typeof(object)],
                serializerFactory: system => new HyperionSerializer(system))
            .AddHocon(
                HyperionSerializer.DefaultConfiguration(),
                HoconAddMode.Append);

        return builder;
    }
}