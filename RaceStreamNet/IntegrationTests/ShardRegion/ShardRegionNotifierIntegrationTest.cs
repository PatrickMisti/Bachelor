using Akka.Actor;
using Akka.Cluster;
using Akka.Cluster.Sharding;
using Akka.Cluster.Tools.PublishSubscribe;
using Akka.Configuration;
using Akka.TestKit.Xunit2;
using DriverShardHost.Config;
using Infrastructure.General;
using Infrastructure.Shard.Messages.Notification;
using Infrastructure.Shard.Messages.RequestMessages;
using Infrastructure.Shard.Messages.ResponseMessage;
using Infrastructure.Testing;
using IntegrationTests.Mock;
using IntegrationTests.ShardRegion.DemoActors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace IntegrationTests.ShardRegion;

public class ShardRegionNotifierIntegrationTest : TestKit, IAsyncLifetime
{
    private IHost? _backendHost;
    private ActorSystem? _proxySystem;
    private IActorRef? _proxyRegion;

    private const string ClusterName = "cluster-system";
    private const string RoleBackend = "backend";
    private const string ShardName = "driver";
    private static readonly int[] BackendPort = [5000, 6000];

    private readonly TimeSpan _timeout = TimeSpan.FromSeconds(5);

    public async Task InitializeAsync()
    {
        // ========== Host ===============
        var akkaHc = new AkkaHostingConfig
        {
            ClusterName = ClusterName,
            Role = RoleBackend,
            ShardName = ShardName,
            Port = BackendPort[0]
        };

        _backendHost = Host.CreateDefaultBuilder()
            .ConfigureServices(services => services.ConfigureShardRegion(akkaHc))
            .Build();

        await _backendHost.StartAsync();

        var system = _backendHost.Services.GetRequiredService<ActorSystem>();
        await MemberUpUtilities.WaitForMemberUp(system, RoleBackend, TimeSpan.FromSeconds(30), Cluster.Get(system));

        // ========== Client ============

        var proxyHocon = ConfigurationFactory.ParseString($@"
        akka {{
          actor.provider = cluster
          remote.dot-netty.tcp.hostname = ""localhost""
          remote.dot-netty.tcp.port = 0
          cluster.roles = [""frontend""]
          cluster.seed-nodes = [""akka.tcp://{ClusterName}@localhost:{BackendPort[0]}"",""akka.tcp://{ClusterName}@localhost:{BackendPort[1]}""]
          log-info = on
          akka.actor.serialize-messages = on
          akka.remote.log-serialization-warnings = on
          akka.loglevel = DEBUG
          akka.stdout-loglevel = DEBUG
        }}").WithFallback(ClusterSharding.DefaultConfig());

        _proxySystem = ActorSystem.Create(ClusterName, proxyHocon);

        _proxyRegion = await ClusterSharding.Get(_proxySystem).StartProxyAsync(
            typeName: ShardName,
            role: null!,
            messageExtractor: new DriverMessageExtractorTest());

        await MemberUpUtilities.WaitForMemberUp(
            _proxySystem, 
            role: "frontend",
            TimeSpan.FromSeconds(10),
            Cluster.Get(_proxySystem));
    }

    public async Task DisposeAsync()
    {
        if (_proxySystem is not null) await _proxySystem.Terminate();
        if (_backendHost is not null) await _backendHost.StopAsync();
    }

    [Fact]
    public async Task Create_Driver_And_Ask_Notifier_For_Information()
    {
        var probe = CreateTestProbe();
        var actor = DemoApiActor.Props(probe);
        _proxySystem!.ActorOf(actor, "demo-api-actor");

        var mock = MockEntities.MOCK_UPDATE_DRIVER_TELEMETRY;
        var suc = await _proxyRegion.Ask<Status.Success>(mock, _timeout);

        Assert.NotNull(suc);
        var expect = probe.ExpectMsg<NotifyDriverStateMessage>(_timeout);
        Assert.Equal(mock.DriverId, expect.DriverId);
        Assert.Equal(mock.LapNumber, expect.State.LapNumber);
    }

    [Fact]
    public async Task Check_If_Created_Drive_Is_Available()
    {
        // Arrange
        var probe = CreateTestProbe();
        var demoApi = _proxySystem!.ActorOf(DemoApiActor.Props(probe), "demo-api-actor");

        // 1) Warmup + „Ready“-Barriere: Telemetrie erzeugt Entity UND triggert PubSub-Flow
        var mock = MockEntities.MOCK_UPDATE_DRIVER_TELEMETRY;

        // an die ShardRegion (Proxy) senden -> Entity wird erstellt/aktualisiert
        var suc = await _proxyRegion!.Ask<Status.Success>(mock, _timeout);
        Assert.NotNull(suc);

        // 2) Verifiziere, dass PubSub bereits „lebt“ (Notify kommt an)
        //    -> Das stellt sicher, dass der TelemetryRegionHandler und der DemoApiActor bereits subscribed sind.
        var notify = probe.ExpectMsg<NotifyDriverStateMessage>(_timeout);
        Assert.Equal(mock.DriverId, notify.DriverId);
        Assert.Equal(mock.LapNumber, notify.State.LapNumber);

        // 3) Jetzt State abfragen – der DemoApiActor published GetDriverStateRequest
        //    und erwartet eine GetDriverStateResponse (der Handler antwortet an msg.ActorRef)
        demoApi.Tell(new GetDriverStateRequest(mock.DriverId));

        // 4) Antwort einsammeln (der DemoApiActor forwardet sie ans probe)
        var res = probe.ExpectMsg<GetDriverStateResponse>(_timeout);

        // Assert
        Assert.Equal(mock.DriverId, res.DriverId);
        Assert.NotNull(res.DriverState);
        Assert.Equal(notify.State.LapNumber, res.DriverState!.LapNumber);
    }

    [Fact]
    public void Check_If_Actor_Is_Registered_In_Cluster()
    {
        // todo check if actor is registered in cluster
        // make a GetDriverStateRequest and check if the driver
        // is not available and after that should also not available
    }

    [Fact] // not working but with systemtest it is clear that it is working
    public async Task Check_If_Created_Driver_Is_Available()
    {
        var probe = CreateTestProbe();

        // Entity anlegen / warm machen
        var mock = MockEntities.MOCK_UPDATE_DRIVER_TELEMETRY;
        var ack = await _proxyRegion!.Ask<Status.Success>(mock, _timeout);
        Assert.NotNull(ack);

        // deterministisch 1:1 an EIN Backend-Subscriber senden
        var mediator = DistributedPubSub.Get(_proxySystem!).Mediator;
        mediator.Tell(
            new Send("/topic/backend",
                new GetDriverStateRequest(mock.DriverId), localAffinity: false),
            probe.Ref); // <- Antwort direkt ans probe

        var res = probe.ExpectMsg<GetDriverStateResponse>(_timeout);
        Assert.Equal(mock.DriverId, res.DriverId);
        Assert.NotNull(res.DriverState);
        Assert.Equal(mock.LapNumber, res.DriverState!.LapNumber);
    }
}