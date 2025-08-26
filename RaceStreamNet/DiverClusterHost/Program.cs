using DiverShardHost.Config;
using Infrastructure.General;

var builder = Host.CreateApplicationBuilder(args);
var defaultPort = 5000;

// Akka.NET hosting configuration
var akkaHc = new AkkaHostingConfig
{
    Port = PortChecker.CheckPort(defaultPort),
    Role = ClusterMemberEnum.Backend.ToStr()
};

// Serilog init
builder.CreateLoggingAdapter();

// Configure Akka.NET
builder.Services.ConfigureShardRegion(akkaHc);

var app = builder.Build();
app.Run();
