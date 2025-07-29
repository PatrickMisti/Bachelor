using Akka.Actor;
using Akka.TestKit.Xunit2;
using DiverClusterHost.Shared;
using Infrastructure.General;
using Serilog;
using Tests.Utilities;
using Xunit;

namespace Tests.Cluster;

public class TestClusterShard : TestKit
{
    private readonly string _pathToHoconFile = "C:\\Programming\\Bachelor\\RaceStreamNet\\DiverClusterHost\\akka.conf";

    [Fact]
    public async Task ClusterController_Starts_ActorSystem_And_Handles_Messages()
    {
        var controller = new ClusterController();
        await controller.Start(path: _pathToHoconFile);

        var region = controller.GetShardRegion();

        var response = await region.Ask<string>(
            new DriverData { DriverId = "Vers" },
            TimeSpan.FromSeconds(5)); // Timeout unbedingt angeben

        Assert.Equal("Received driver with ID Vers", response);

        await controller.Stop();
    }

    // Hilfsmethode zum Zugriff auf private Felder (z. B. _actorSystem)
    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (T)field!.GetValue(instance)!;
    }

    [Fact]
    public async Task ClusterController_Share_Information()
    {
        var logger = TestLogger.CreateLogger<ClusterController>();
        var controllerA = new ClusterController(logger);
        await controllerA.Start(actorSystemName: "DriverCluster", path: _pathToHoconFile);

        var controllerB = new ClusterController(logger);
        await controllerB.Start(actorSystemName: "DriverCluster", path: _pathToHoconFile);

        var regionA = controllerA.GetShardRegion();
        var regionB = controllerB.GetShardRegion();

        var response1 = await regionA.Ask<string>(
            new DriverData { DriverId = "Ver" },
            TimeSpan.FromSeconds(5));

        var response2 = await regionB.Ask<string>(
            new DriverData { DriverId = "Ver" },
            TimeSpan.FromSeconds(5));

        Assert.Equal(response1, response2); // Sollte gleich sein
        Assert.Contains("Ver", response1);
    }
}