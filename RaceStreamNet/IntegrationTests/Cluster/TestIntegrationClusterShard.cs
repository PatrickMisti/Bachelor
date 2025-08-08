using Akka.Actor;
using Akka.Cluster.Sharding;
using Akka.Configuration;
using Akka.TestKit.Xunit2;
using Infrastructure.Cluster.Base;
using Infrastructure.General;
using Xunit;

namespace IntegrationTests.Cluster;

public class TestIntegrationClusterShard : TestKit
{

    private ActorSystem? _proxySystem;
    private IActorRef? _proxyRegion;

    [Fact]
    public async Task Should_Send_DriverData_Through_Proxy()
    {
        var hocon = ConfigurationFactory.ParseString($@"
            akka {{
              actor.provider = cluster
              remote.dot-netty.tcp {{
                hostname = ""localhost""
                port = 0
              }}
              cluster.seed-nodes = [""akka.tcp://cluster-system@localhost:5000""]
              cluster.roles = [""frontend""]
            }}").WithFallback(ClusterSharding.DefaultConfig());

        _proxySystem = ActorSystem.Create("cluster-system", hocon);

        // Proxy zur echten ShardRegion "driver"
        _proxyRegion = await ClusterSharding.Get(_proxySystem).StartProxyAsync(
            typeName: "driver",
            role: string.Empty, // oder null, falls ohne Einschränkung
            messageExtractor: new DriverMessageExtractor()
        );


        var msg = new DriverData
        {
            DriverId = "Ver"
        };
        var response = await _proxyRegion.Ask<string>(msg); // Antwort geht an Probe

        Assert.NotNull(response);
        Assert.Contains("Ver", response);

        Console.WriteLine("Output from actor is");
        // z. B. Antwortprüfung mit ReceiveTimeout o. Ä.
        await Task.Delay(1000);

        if (_proxySystem is not null)
            await _proxySystem.Terminate();
    }
}