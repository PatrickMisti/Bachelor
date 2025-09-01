using Akka.Actor;
using Akka.Actor.Dsl;
using Akka.Cluster;
using Akka.Configuration;
using Akka.TestKit;
using Akka.TestKit.Xunit2;
using ClusterCoordinator.Config;
using DriverShardHost.Config;
using Infrastructure.Coordinator;
using Infrastructure.Coordinator.PubSub;
using Infrastructure.General;
using Infrastructure.Testing;
using IntegrationTests.Coordinator.Actor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;
using Xunit.Abstractions;

namespace IntegrationTests.Coordinator;

public class CoordinatorIntegrationTests: TestKit, IAsyncLifetime
{
    private static readonly string TestConfig = $"""
                                                 akka.loglevel = "DEBUG"
                                                 akka.actor.provider = "Akka.Cluster.ClusterActorRefProvider, Akka.Cluster"
                                                 akka.remote.dot-netty.tcp.hostname = "localhost"
                                                 akka.remote.dot-netty.tcp.port = 0
                                                 akka.scheduler.implementation = "Akka.TestKit.TestScheduler, Akka.TestKit"
                                                 akka.cluster.pub-sub.name = "distributedPubSubMediator"
                                                 akka.cluster.roles = ["ingress"]
                                                 """;

    private Cluster _cluster;
    private IActorRef? _ingressDemoRef;

    private TestProbe _probe;
    
    private HostApplicationBuilder _appCoordinator;
    private HostApplicationBuilder _appShard;

    private IHost _serviceShard;
    private IHost _serviceCoordinator;

    public CoordinatorIntegrationTests(ITestOutputHelper helper) : base(ActorSystem.Create("cluster-system", ConfigurationFactory.ParseString(TestConfig)), helper)
    {
        _cluster = Cluster.Get(Sys);

        _appCoordinator = Host.CreateApplicationBuilder();
        _appCoordinator.Services.ConfigureCoordinator(new AkkaHostingConfig
        {
            Port = 6000,
            Role = ClusterMemberEnum.Controller.ToStr()
        },"Test");

        _appShard = Host.CreateApplicationBuilder();
        _appShard.Services.ConfigureShardRegion(new AkkaHostingConfig
        {
            Port = 5000,
            Role = ClusterMemberEnum.Backend.ToStr()
        });

        _serviceShard = _appShard.Build();
        _serviceCoordinator = _appCoordinator.Build();
    }

    public async Task InitializeAsync()
    {
        await _serviceCoordinator.StartAsync();
        await _serviceShard.StartAsync();

        
        var seeds = new[]
        {
            Address.Parse("akka.tcp://cluster-system@127.0.0.1:5000"),
            Address.Parse("akka.tcp://cluster-system@127.0.0.1:6000"),
        };

        _cluster.JoinSeedNodes(seeds);

        await MemberUpUtilities.WaitForMemberUp(_serviceCoordinator.Services.GetRequiredService<ActorSystem>(), TimeSpan.FromSeconds(3), _cluster);
        await MemberUpUtilities.WaitForMemberUp(_serviceShard.Services.GetRequiredService<ActorSystem>(), TimeSpan.FromSeconds(3), _cluster);



        //await MemberUpUtilities.WaitForMemberUp(Sys, ClusterMemberEnum.Controller.ToStr(), TimeSpan.FromSeconds(2), _cluster);
        //await MemberUpUtilities.WaitForMemberUp(Sys, ClusterMemberEnum.Controller.ToStr(), TimeSpan.FromSeconds(2), _cluster);



        _probe = CreateTestProbe();

        _ingressDemoRef = Sys.ActorOf(Props.Create(() => new IngressGhostActor(_probe, Output)));
        await Task.Delay(500);
    }

    public async Task DisposeAsync()
    {
        await _serviceCoordinator.StopAsync();
        await _serviceShard.StopAsync();
    }


    //[Fact]
    // Not working because of some issue with TestKit and multiple ActorSystems
    public async Task Start_Shard_And_Ingress_Will_Get_Notified()
    {
        //await _serviceShard.StartAsync();
        await Task.Delay(2000);

        _ingressDemoRef.Tell(TestGetInfoOfShard.Instance);
        var result = _probe.ExpectMsg<IngressConnectivityResponse>(TimeSpan.FromSeconds(5));
        Assert.True(result.ShardAvailable);
    }
}