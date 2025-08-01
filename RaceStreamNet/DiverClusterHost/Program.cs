using DiverClusterHost.Cluster;
using DiverClusterHost.Cluster.Actors;
using Infrastructure.Cluster.Interfaces;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

// Remove Microsoft Logger
builder.Logging.ClearProviders();

// Read Serilog from appsettings.json
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration) 
    .CreateLogger();

// set Serilog active
builder.Logging.AddSerilog(Log.Logger);

builder.Services.AddSingleton<IClusterController, ClusterController>();

// needed for DI and Akka.net
builder.Services.AddTransient<DriverActor>();
builder.Services.AddTransient<ClusterMembershipListener>();


var host = builder.Build();

var controller = (ClusterController)host.Services.GetRequiredService<IClusterController>();
await controller.Start();

host.Run();
