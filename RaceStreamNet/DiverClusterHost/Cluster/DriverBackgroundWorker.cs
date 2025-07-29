using Akka.Actor;
using Akka.Cluster.Sharding;
using Akka.Configuration;
using Akka.DependencyInjection;
using Infrastructure.Cluster.Basis;
using Infrastructure.General;

namespace DiverClusterHost.Cluster
{
    public class DriverBackgroundWorker(ILogger<DriverBackgroundWorker> logger, IServiceProvider sp) : BackgroundService
    {
        private ActorSystem _actorSystem = null!;
        private readonly int _defaultSeedNode = 5000; // Default port for the ActorSystem

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("Worker started – initialize ActorSystem...");
            int port = PortChecker.CheckPort(_defaultSeedNode);
            logger.LogInformation("ActorSystem is listening at Port : {port}", port);

            // Load Akka-Configuration
            var hoconTemplate = await File.ReadAllTextAsync("akka.conf", stoppingToken);
            var hoconWithPort = hoconTemplate
                .Replace("{{PORT}}", port.ToString())
                .Replace("{{SEED_PORT}}", _defaultSeedNode.ToString());
            var config = ConfigurationFactory.ParseString(hoconWithPort);


            // Create Akka + DI Setup
            var bootstrap = BootstrapSetup.Create().WithConfig(config);
            var diSetup = DependencyResolverSetup.Create(sp);
            var actorSystemSetup = bootstrap.And(diSetup);

            _actorSystem = ActorSystem.Create("DriverClusterNode", actorSystemSetup);
            
            await StartActorSystem(_actorSystem);
            
            // Wait till end
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        private async Task StartActorSystem(ActorSystem actorSystem)
        {
            var resolver = DependencyResolver.For(actorSystem);
            // Start ShardRegion
            await ClusterSharding.Get(actorSystem).StartAsync(
                typeName: "driver",                                                 // Name of the ShardRegion
                entityProps: resolver.Props<DriverActor>(),                         // Props for the entity actor
                settings: ClusterShardingSettings.Create(actorSystem),              // Sharding settings
                messageExtractor: new DriverMessageExtractor()                      // Message extractor for the ShardRegion
            );

            logger.LogInformation("ShardRegion 'driver' started. Akka-System ready.");
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("ActorSystem shutdown...");
            await CoordinatedShutdown.Get(_actorSystem).Run(CoordinatedShutdown.ClrExitReason.Instance);
        }
    }
}
