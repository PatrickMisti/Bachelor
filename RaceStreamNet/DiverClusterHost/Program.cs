using DiverClusterHost.Cluster;
using DiverClusterHost.Shared;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

builder.Logging.AddSerilog(); // not needed to add Log.Logger

builder.Services.AddSingleton<ClusterController>();
// needed for DI and Akka.net
builder.Services.AddTransient<DriverActor>();

var host = builder.Build();

var controller = host.Services.GetRequiredService<ClusterController>();
await controller.Start();

host.Run();
