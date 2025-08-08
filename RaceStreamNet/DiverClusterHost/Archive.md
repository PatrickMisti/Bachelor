# ClusterShard

## V1 Di + Akka.net

Somit wird das Aktorensystem in die DI gelegt.

ClusterController.cs
```c#
public class ClusterController : IClusterController
{
    private ActorSystem? _actorSystem;
    private readonly int _defaultSeedNode = 5000;         // Default port for the ActorSystem
    private readonly string _regionName = "driver";       // Name of the ShardRegion
    private IActorRef? _shardRegion;                      // Reference to the ShardRegion

    private readonly IServiceProvider? _serviceProvider;
    private readonly ILogger<ClusterController> _logger;

    private readonly ClusterMembershipListener? _membershipListener;
    private int Port { set; get; }

    private readonly string _defaultHoconFilePath = "akka.conf";


    public ClusterController() : this(CreateDefaultLogger())
    {
    }

    private static ILogger<ClusterController> CreateDefaultLogger()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .CreateLogger();

        var factory = LoggerFactory.Create(x => x.AddSerilog(Log.Logger, dispose: true));
        return factory.CreateLogger<ClusterController>();
    }

    public ClusterController(ILogger<ClusterController> logger) : this(logger, null!)
    {
    }

    public ClusterController(ILogger<ClusterController> logger, IServiceProvider sp)
    {
        _logger = logger;
        _serviceProvider = sp;
    }

    private ActorSystemSetup AddActorSystemToDi(Config config)
    {
        if (_serviceProvider is null) 
            throw new InvalidOperationException("ServiceProvider is not initialized.");

        _logger.LogDebug("Creating ActorSystem with DI ...");
        // Create Akka + DI Setup
        var bootstrap = BootstrapSetup.Create().WithConfig(config);
        var diSetup = DependencyResolverSetup.Create(_serviceProvider);
        var actorSystemSetup = bootstrap.And(diSetup);

        return actorSystemSetup;
    }

    public async Task Start(string actorSystemName = "DriverClusterNode", string? path = null, bool withDi = true)
    {
        _logger.LogInformation("Starting ActorSystem: {ActorSystemName} on Port: {Port} Seed-Node-Port: {Seed}",
            actorSystemName, Port, _defaultSeedNode);

        var (resolver, log) = await RegisterAllActors(withDi, actorSystemName, path);
        
        // Start ShardRegion
        _shardRegion = await ClusterSharding.Get(_actorSystem!).StartAsync(
            typeName: _regionName,
            entityProps: resolver is null
                ? Props.Create(() => new DriverActor(log.CreateLogger<DriverActor>()))
                : resolver.Props<DriverActor>(),                       // Use DI if available
            settings: ClusterShardingSettings.Create(_actorSystem),    // Sharding settings
            messageExtractor: new DriverMessageExtractor()             // Message extractor for the ShardRegion
        );

        _logger.LogInformation("ShardRegion '{RegionName}' started. Akka-System ready.",
            _regionName);
    }
    public IActorRef GetShardRegion()
    {
        if (_shardRegion is null)
        {
            _logger.LogWarning("ShardRegion is not initialized. Returning null.");
            return null!;
        }
        return _shardRegion;
    }

    private async Task<(DependencyResolver? resolver, ILoggerFactory factory)> RegisterAllActors(bool withDi, string actorSystemName, string? path)
    {
        var hoconConfig = await AkkaConfigLoader.LoadAsync(
            path ?? _defaultHoconFilePath,
            Port = PortChecker.CheckPort(_defaultSeedNode),
            _defaultSeedNode,
            actorSystemName);

        var log = LoggerFactory.Create(x => x.AddSerilog());
        DependencyResolver? resolver = null;
        Props? memberProps = null;

        if (withDi)
        {
            _actorSystem = ActorSystem.Create(actorSystemName, AddActorSystemToDi(hoconConfig));
            resolver = DependencyResolver.For(_actorSystem);

            memberProps = resolver.Props<ClusterMembershipListener>();
        }

        _actorSystem ??= ActorSystem.Create(actorSystemName, hoconConfig);
        memberProps ??= Props.Create(() => new ClusterMembershipListener(log.CreateLogger<ClusterMembershipListener>()));

        // Register in actor_system

        _actorSystem?.ActorOf(memberProps, "ClusterMemberListener");

        return (resolver,log);
    }

    public async Task Stop()
    {
        if (_actorSystem is null)
        {
            _logger.LogWarning("ActorSystem is not initialized. Cannot stop.");
            return;
        }

        _logger.LogInformation("Shutting down ActorSystem...");
        await CoordinatedShutdown.Get(_actorSystem).Run(CoordinatedShutdown.ClrExitReason.Instance);
        _logger.LogInformation("ActorSystem shutdown complete.");
    }
}
```

um den Controller in die Di zu legen muss in der Startup.cs

```c#
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
```

## V2 Hosting 
Vorteil man start sich den Controller da man es gleich im Startup alles 
eingibt und man somit keine HOCON file laden muss.

### Conclusion

Besser für DriverShardHost sicher Hosting da das Subprojekt
nur sich um den Shard handln soll und sonst nichts
