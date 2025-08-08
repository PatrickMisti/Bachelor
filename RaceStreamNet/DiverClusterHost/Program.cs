using Akka.Cluster.Hosting;
using Akka.Hosting;
using Akka.Logger.Serilog;
using Akka.Remote.Hosting;
using DiverShardHost.Cluster.Actors;
using Infrastructure.Cluster.Base;
using Infrastructure.Cluster.Config;
using Infrastructure.General;
using Serilog;
using Serilog.Events;
using LogLevel = Akka.Event.LogLevel;

var builder = Host.CreateApplicationBuilder(args);

// Serilog init
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog(Log.Logger);

// Akka.NET hosting configuration
var akkaHc = new AkkaHostingConfig
{
    Port = 5000,
    Role = "backend",
    ShardName = "driver",
};

// Configure Akka.NET
builder.Services.AddAkka(akkaHc.ClusterName, (config, setup) =>
{
    config
        .WithRemoting(port: akkaHc.Port, hostname: akkaHc.Hostname)
        .WithClustering(new ClusterOptions
        {
            SeedNodes = akkaHc.SeedNodes,
            Roles = [akkaHc.Role],
        })
        .ConfigureLoggers(logger =>
        {
            logger.LogLevel = LogLevel.InfoLevel;
            logger.ClearLoggers();
            // Logging.GetLogger(context.System, "echo")
            // to use the logger in the actor echo is actor
            logger.AddLogger<SerilogLogger>();
        })
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

var app = builder.Build();
app.Run();
