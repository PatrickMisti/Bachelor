using Akka.Actor;
using Akka.Cluster;
using Akka.Cluster.Sharding;
using Akka.Configuration;
using Akka.TestKit.Xunit2;
using DriverShardHost.Actors.Messages;
using DriverShardHost.Config;
using Infrastructure.General;
using Infrastructure.Shard;
using Infrastructure.Shard.Messages;
using Infrastructure.Shard.Models;
using Infrastructure.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace IntegrationTests.ShardRegion;

public class ShardRegionIntegrationTests : TestKit, IAsyncLifetime
{
    private IHost? _backendHost;
    private ActorSystem? _proxySystem;
    private IActorRef? _proxyRegion;

    private const string ClusterName = "cluster-system";
    private const string RoleBackend = "backend";
    private const string ShardName = "driver";
    private static readonly int[] BackendPort = [5000, 6000];

    public async Task InitializeAsync()
    {
        var akkaHc = new AkkaHostingConfig
        {
            ClusterName = ClusterName,
            Role = RoleBackend,
            ShardName = ShardName,
            Port = BackendPort[0]
        };

        _backendHost = Host.CreateDefaultBuilder()
            .ConfigureServices(services => services.ConfigureShardRegion(akkaHc, new DriverMessageExtractorTest()))
            .Build();

        await _backendHost.StartAsync();

        
        var system = _backendHost.Services.GetRequiredService<ActorSystem>();
        await MemberUpUtilities.WaitForMemberUp(system, RoleBackend, TimeSpan.FromSeconds(30), Cluster.Get(system));
        
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

        await MemberUpUtilities.WaitForMemberUp(_proxySystem, role: "frontend", TimeSpan.FromSeconds(10), Cluster.Get(_proxySystem));
    }

    public async Task DisposeAsync()
    {
        if (_proxySystem is not null) await _proxySystem.Terminate();
        if (_backendHost is not null) await _backendHost.StopAsync();
    }

    [Fact]
    public async Task Proxy_should_send_telemetry_and_fetch_state_from_backend_region()
    {
        var driverId = "DRIVER_44";
        var driverNumber = 1;

        var upsert = new UpdateDriverTelemetry(
            DriverNumber: (uint)driverNumber,
            SessionId: 1111,
            DriverId: driverId,
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

        var ack = await _proxyRegion!.Ask<object>(upsert, TimeSpan.FromSeconds(5));
        Assert.NotNull(ack);

        var resp = await _proxyRegion.Ask<DriverStateMessage>(new GetDriverState(driverNumber, driverNumber), TimeSpan.FromSeconds(10));
        Assert.NotNull(resp);
        Assert.True(resp.IsSuccess);
        Assert.Equal(driverId, resp.DriverId);
        Assert.NotNull(resp.State);
        Assert.Equal(12, resp.State!.LapNumber);
        Assert.Equal(3, resp.State.PositionOnTrack);
        Assert.Equal(TyreCompound.Soft, resp.State.CurrentTyreCompound);
    }

    //[Fact]
    //Not working yet because of Extractor
    // problem it will always generate new driver_actor throw Extractor
    public async Task Proxy_should_return_failure_when_driverId_mismatch()
    {
        var driverId = "DRIVER_45";
        
        var upsert = new UpdateDriverTelemetry(
            DriverNumber: 1,
            SessionId: 1111,
            DriverId: driverId,
            LapNumber: 1,
            PositionOnTrack: 5,
            Speed: 250,
            DeltaToLeader: 0.5,
            TyreLife: 5,
            CurrentTyreCompound: TyreCompound.Medium,
            PitStops: 0,
            LastLapTime: TimeSpan.FromSeconds(90),
            Sector1Time: TimeSpan.FromSeconds(30),
            Sector2Time: TimeSpan.FromSeconds(30),
            Sector3Time: TimeSpan.FromSeconds(30),
            Timestamp: DateTime.UtcNow);

        await _proxyRegion!.Ask<Status>(upsert, TimeSpan.FromSeconds(5));

        var wrongId = 11;
        var resp = await _proxyRegion.Ask<DriverStateMessage>(
            new GetDriverState(wrongId, wrongId),
            TimeSpan.FromSeconds(5));

        Assert.NotNull(resp);
        Assert.False(resp.IsSuccess);
        Assert.Equal($"{wrongId}_{wrongId}", resp.DriverId);
        Assert.NotNull(resp.Error);
        Assert.Null(resp.State);
    }
}