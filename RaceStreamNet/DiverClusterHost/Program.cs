using Akka.Cluster.Hosting;
using Akka.Hosting;
using DiverShardHost.Cluster.Actors;
using DiverShardHost.Config;
using Infrastructure.Cluster.Base;
using Infrastructure.Cluster.Config;
using Infrastructure.General;
using Serilog;
using Serilog.Events;

var builder = Host.CreateApplicationBuilder(args);
var defaultPort = 5000;

// Akka.NET hosting configuration
var akkaHc = new AkkaHostingConfig
{
    Port = PortChecker.CheckPort(defaultPort),
    Role = "backend"
};

// Serilog init
builder.CreateLoggingAdapter();

// Configure Akka.NET
builder.Services.ConfigureShardRegion(akkaHc);

var app = builder.Build();
app.Run();
