using DriverTelemetryIngress.Config;
using Infrastructure.General;

var builder = Host.CreateApplicationBuilder(args);
var defaultPort = 7000;

// Akka.NET hosting configuration
var akkaHc = new AkkaHostingConfig
{
    Port = PortChecker.CheckPort(defaultPort),
    Role = ClusterMemberEnum.Ingress.ToStr()
};

// Serilog init
builder.CreateLoggingAdapter();

// Configure Akka.NET
builder.Services.ConfigureStreams(akkaHc);

var host = builder.Build();
host.Run();
