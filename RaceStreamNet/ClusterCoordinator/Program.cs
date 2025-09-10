using ClusterCoordinator.Config;
using Infrastructure.General;

var builder = Host.CreateApplicationBuilder(args);
Console.Title = ClusterMemberEnum.Controller.ToStr();

var connectionString = "./DB/akka-cluster.db";
var defaultPort = 6000;

var akkaHc = new AkkaHostingConfig
{
    Port = PortChecker.CheckPort(defaultPort),
    Role = ClusterMemberEnum.Controller.ToStr(),
};

// Serilog init
builder.CreateLoggingAdapter();

// Config ClusterCoordinator with Singleton and Listeners
builder.Services.ConfigureCoordinator(akkaHc, connectionString);

var app = builder.Build();
app.Run();
