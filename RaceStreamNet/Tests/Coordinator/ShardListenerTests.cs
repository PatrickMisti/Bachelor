using Akka.Actor;
using Akka.Cluster.Tools.PublishSubscribe;
using Akka.Hosting;
using Akka.TestKit;
using Akka.TestKit.Xunit2;
using ClusterCoordinator.Actors;
using ClusterCoordinator.Actors.Listeners;
using ClusterCoordinator.Actors.Messages;
using ClusterCoordinator.Actors.Messages.ClusterMemberEvents;
using ClusterCoordinator.Actors.Messages.Request;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Tests.Coordinator;

public sealed class ShardListenerTests : TestKit
{
    // TestKit-Config: Cluster-Provider + TestScheduler
    private static readonly string TestConfig = """
                                                akka.loglevel = "DEBUG"
                                                akka.actor.provider = "Akka.Cluster.ClusterActorRefProvider, Akka.Cluster"
                                                akka.scheduler.implementation = "Akka.TestKit.TestScheduler, Akka.TestKit"
                                                akka.cluster.pub-sub.name = "distributedPubSubMediator"
                                                """;


    private static Address Address(int port = 5000, string system = "cluster-system", string host = "127.0.0.1")
        => new ("akka.tcp", system, host, port);

    private readonly TestProbe _controllerProbe;
    private readonly IActorRef _listener;
    private readonly Mock<IRequiredActor<ClusterController>> _mockClusterController;

    public ShardListenerTests(ITestOutputHelper output)
        : base(TestConfig, output)
    {
        _controllerProbe = CreateTestProbe();

        _mockClusterController = new Mock<IRequiredActor<ClusterController>>();
        _mockClusterController.SetupGet(x => x.ActorRef).Returns(_controllerProbe.Ref);

        _listener = Sys.ActorOf(Props.Create(() => new ShardListener(_mockClusterController.Object)));

        var topic = "controller";
        var group = "group-controller";     
        _listener.Tell(new SubscribeAck(new Subscribe(topic, _listener, group: null)));
        _listener.Tell(new SubscribeAck(new Subscribe(topic, _listener, group: group)));
    }

    [Fact]
    public void Increase_emits_online_true_after_debounce()
    {
        // Warten bis der Debounce-Timer wirklich geplant wurde (hier 2x wegen 2 Increase)
        EventFilter.Debug(start: "Debouncing shard count update").Expect(2, () =>
        {
            _listener.Tell(new IncreaseClusterMember(Address()));
            _listener.Tell(new IncreaseClusterMember(Address(5001)));
        });

        // Debounce „abrennen“ (TestScheduler: Advance, sonst Sleep)
        FlushDebounce();

        // WICHTIG: auf dem Controller-Probe prüfen
        var msg = _controllerProbe.ExpectMsg<ShardConnectionUpdateMessage>(TimeSpan.FromSeconds(1));
        Assert.True(msg.IsShardOnline);
    }

    [Fact]
    public void Increase_then_Decrease_emits_online_false_after_debounce()
    {
        var addr = Address(5000);

        EventFilter.Debug(start: "Debouncing shard count update").Expect(1, () =>
            _listener.Tell(new IncreaseClusterMember(addr)));
        FlushDebounce();
        _controllerProbe.ExpectMsg<ShardConnectionUpdateMessage>(m => Assert.True(m.IsShardOnline));

        EventFilter.Debug(start: "Debouncing shard count update").Expect(1, () =>
            _listener.Tell(new DecreaseClusterMember(addr)));
        FlushDebounce();
        _controllerProbe.ExpectMsg<ShardConnectionUpdateMessage>(m => Assert.False(m.IsShardOnline));
    }

    [Fact]
    public async Task Check_If_ConnectionRequest_Response_ShardConnectionUpdateMessage()
    {
        _listener.Tell(new IncreaseClusterMember(Address()));
        var exp1 = await _listener.Ask<ShardConnectionUpdateMessage>(ShardConnectionRequest.Instance);
        Assert.True(exp1.IsShardOnline);

        _listener.Tell(new DecreaseClusterMember(Address()));
        var exp2 = await _listener.Ask<ShardConnectionUpdateMessage>(ShardConnectionRequest.Instance);
        Assert.False(exp2.IsShardOnline);
    }

    private void FlushDebounce(TimeSpan? delay = null)
    {
        var d = delay ?? TimeSpan.FromMilliseconds(350); // > 200ms
        if (Sys.Scheduler is TestScheduler ts)
            ts.Advance(d);
        else
            System.Threading.Thread.Sleep(d);
    }
}