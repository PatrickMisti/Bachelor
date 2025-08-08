using Akka.Cluster.Hosting;
using Akka.Hosting;
using DiverShardHost.Cluster.Actors;
using Infrastructure.Cluster.Base;
using Infrastructure.Cluster.Config;
using Infrastructure.General;
using Serilog;
using Serilog.Events;

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
    Role = "backend"
};

// Configure Akka.NET
builder.Services.AddAkka(akkaHc.ClusterName, config =>
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

var app = builder.Build();
app.Run();
