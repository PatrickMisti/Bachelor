using Akka.Cluster.Hosting;
using Akka.Hosting;
using Akka.Logger.Serilog;
using Akka.Remote.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using LogLevel = Akka.Event.LogLevel;
using Serilog;

namespace Infrastructure.General;

public static class AkkaHostingExtension
{
    public static AkkaConfigurationBuilder UseRemoteCluster(this AkkaConfigurationBuilder builder,
        AkkaHostingConfig config)
    {
        builder
            .WithRemoting(
                port: config.Port,
                hostname: config.Hostname)
            .WithClustering(
                new ClusterOptions
                {
                    SeedNodes = config.SeedNodes,
                    Roles = [config.Role],
                })
            .WithDistributedPubSub(role: null!);

        return builder;
    }

    public static AkkaConfigurationBuilder UseAkkaLogger(this AkkaConfigurationBuilder builder)
    {

        builder.ConfigureLoggers(logger =>
        {
            logger.LogLevel = LogLevel.InfoLevel;
            logger.ClearLoggers();
            // Logging.GetLogger(context.System, "echo")
            // to use the logger in the actor echo is actor
            logger.AddLogger<SerilogLogger>();
        });

        return builder;
    }

    public static IHostApplicationBuilder CreateLoggingAdapter(this IHostApplicationBuilder builder)
    {
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration)
            .CreateLogger();

        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(Log.Logger);

        return builder;
    }
}