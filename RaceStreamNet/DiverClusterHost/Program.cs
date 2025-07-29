using DiverClusterHost.Cluster;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .CreateLogger();

builder.Logging.AddSerilog(); // not needed to add Log.Logger

builder.Services.AddHostedService<DriverBackgroundWorker>();

var host = builder.Build();
host.Run();
