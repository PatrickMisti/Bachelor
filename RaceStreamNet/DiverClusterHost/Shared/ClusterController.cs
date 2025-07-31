using Akka.Actor;
using Akka.Actor.Setup;
using Akka.Cluster.Sharding;
using Akka.Configuration;
using Akka.DependencyInjection;
using DiverClusterHost.Cluster;
using Infrastructure.Cluster.Basis;
using Infrastructure.General;
using Serilog;

namespace DiverClusterHost.Shared;

public class ClusterController
{
    private ActorSystem? _actorSystem;
    private readonly int _defaultSeedNode = 5000;         // Default port for the ActorSystem
    private readonly string _regionName = "driver";       // Name of the ShardRegion
    private IActorRef? _shardRegion;                      // Reference to the ShardRegion

    private readonly IServiceProvider? _serviceProvider;
    private readonly ILogger<ClusterController> _logger;
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

    public ClusterController(ILogger<ClusterController> logger)
    {
        _logger = logger;
        Console.WriteLine("ClusterController ILogger");
    }
    public ClusterController(ILogger<ClusterController> logger, IServiceProvider sp)
    {
        _logger = logger;
        _serviceProvider = sp;
        Console.WriteLine("ClusterController ILogger and sp");
    }

    private async Task<Config> LoadHoconFile(string path, string actorName)
    {
        _logger.LogDebug("Loading HOCON file and Checking Port ...");
        Port = PortChecker.CheckPort(_defaultSeedNode);
        _logger.LogDebug("Found free port: {p}", Port);
        if (!File.Exists(path))
            throw new FileNotFoundException($"HOCON file not found at path: {path}");

        // Load the HOCON file and replace placeholders
        _logger.LogDebug("Loading Hocon file ...");
        var hoconTemplate = await File.ReadAllTextAsync(path);
        var hoconWithPort = hoconTemplate
            .Replace("{{PORT}}", Port.ToString())
            .Replace("{{SEED_PORT}}", _defaultSeedNode.ToString())
            .Replace("{{CLUSTER_NAME}}", actorName);

        return ConfigurationFactory.ParseString(hoconWithPort);
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
        var hoconConfig = await LoadHoconFile(path ?? _defaultHoconFilePath, actorSystemName);

        _logger.LogInformation("Starting ActorSystem: {ActorSystemName} on Port: {Port} Seed-Node-Port: {Seed}",
            actorSystemName, Port, _defaultSeedNode);

        _actorSystem = withDi 
            ? ActorSystem.Create(actorSystemName, AddActorSystemToDi(hoconConfig)) 
            : ActorSystem.Create(actorSystemName, hoconConfig);

        var resolver = withDi ? DependencyResolver.For(_actorSystem) : null;

        var log = LoggerFactory.Create(x => x.AddSerilog()).CreateLogger<DriverActor>();
        // Start ShardRegion
        _shardRegion = await ClusterSharding.Get(_actorSystem).StartAsync(
            typeName: _regionName,
            entityProps: resolver is null
                ? Props.Create(() => new DriverActor(log))
                : resolver.Props<DriverActor>(), // Use DI if available
            settings: ClusterShardingSettings.Create(_actorSystem), // Sharding settings
            messageExtractor: new DriverMessageExtractor() // Message extractor for the ShardRegion
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