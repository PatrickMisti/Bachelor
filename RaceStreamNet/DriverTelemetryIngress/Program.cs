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

Console.Title = ClusterMemberEnum.Ingress.ToStr() + " " + akkaHc.Port;

// Serilog init
builder.CreateLoggingAdapter();

// Configure Akka.NET
builder.Services
    .ConfigureHttp()
    .ConfigureStreams(akkaHc);

var host = builder.Build();
host.Run();
