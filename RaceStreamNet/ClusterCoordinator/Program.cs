using Akka.Cluster.Hosting;
using Akka.Hosting;
using ClusterCoordinator;
using Infrastructure.Cluster.Config;
using Infrastructure.General;
using Serilog;
using Serilog.Events;


var shardMonitorControllerName = "shard-monitor";

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

var akkaHc = new AkkaHostingConfig
{
    Port = 6000,
    Role = "controller",
};

builder.Services.AddAkka(akkaHc.ClusterName, config =>
{
    config
        .UseRemoteCluster(akkaHc)
        .UseAkkaLogger()
        .WithSingleton<ClusterController>(
            singletonName: akkaHc.Role,
            propsFactory: (_, _, resolver) => resolver.Props<ClusterController>(),
            options: new ClusterSingletonOptions { Role = akkaHc.Role }
        ).WithActors((system, registry, resolver) =>
        {
            var controller = registry.Get<ClusterController>();

            var monitorShard = resolver.Props<ShardMonitorController>(controller);
            var shardMonitorActor = system.ActorOf(monitorShard, shardMonitorControllerName);

            registry.Register<ShardMonitorController>(shardMonitorActor);
        });
});

var app = builder.Build();
app.Run();
