using ClusterCoordinator.Config;
using Infrastructure.Cluster.Config;
using Infrastructure.General;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = "./DB/akka-cluster.db";
var defaultPort = 6000;

var akkaHc = new AkkaHostingConfig
{
    Port = PortChecker.CheckPort(defaultPort),
    Role = "controller",
};

// Serilog init
builder.CreateLoggingAdapter();

// Config ClusterCoordinator with Singleton and Listeners
builder.Services.ConfigureCoordinator(akkaHc, connectionString);

var app = builder.Build();
app.Run();
