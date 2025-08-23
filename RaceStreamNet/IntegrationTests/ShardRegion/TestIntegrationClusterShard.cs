using Akka.Actor;
using Akka.Cluster.Sharding;
using Akka.Cluster.Tools.PublishSubscribe;
using Akka.Configuration;
using Akka.TestKit.Xunit2;
using Infrastructure.Cluster.Messages.RequestMessages;
using Infrastructure.Cluster.Messages.ResponseMessage;
using Infrastructure.Models;
using Infrastructure.Shard.Messages;
using Infrastructure.Testing;
using IntegrationTests.Mock;
using System.Threading;
using Xunit;

namespace IntegrationTests.ShardRegion;

public class TestIntegrationClusterShard : TestKit
{

    private ActorSystem? _proxySystem;
    private IActorRef? _proxyRegion;

    // Only run this when service is running
    //[Fact]
    public async Task Should_Send_DriverData_Through_Proxy()
    {
        var hocon = ConfigurationFactory.ParseString($@"
            akka {{
              actor.provider = cluster
              remote.dot-netty.tcp {{
                hostname = ""localhost""
                port = 0
              }}
              cluster.seed-nodes = [""akka.tcp://cluster-system@localhost:5000"",""akka.tcp://cluster-system@localhost:6000""]
              cluster.roles = [""frontend""]
            }}").WithFallback(ClusterSharding.DefaultConfig());

        _proxySystem = ActorSystem.Create("cluster-system", hocon);

        // Proxy zur echten ShardRegion "driver"
        _proxyRegion = await ClusterSharding.Get(_proxySystem).StartProxyAsync(
            typeName: "driver",
            role: string.Empty, // oder null, falls ohne Einschränkung
            messageExtractor: new DriverMessageExtractorTest()
        );


        var msg = new UpdateDriverTelemetry(
            DriverId: "Ver",
            LapNumber: 12,
            PositionOnTrack: 3,
            Speed: 278.5,
            DeltaToLeader: 1.234,
            TyreLife: 10,
            CurrentTyreCompound: TyreCompound.Soft,
            PitStops: 1,
            LastLapTime: TimeSpan.FromSeconds(92.345),
            Sector1Time: TimeSpan.FromSeconds(29.1),
            Sector2Time: TimeSpan.FromSeconds(31.2),
            Sector3Time: TimeSpan.FromSeconds(32.0),
            Timestamp: DateTime.UtcNow);

        var response = await _proxyRegion.Ask<string>(msg);

        Assert.NotNull(response);
        Assert.Contains("Ver", response);

        // z. B. Antwortprüfung mit ReceiveTimeout o. Ä.
        await Task.Delay(1000);

        if (_proxySystem is not null)
            await _proxySystem.Terminate();
    }
}