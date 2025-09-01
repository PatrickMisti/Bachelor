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
using ClusterCoordinator.Actors.Messages.Response;
using Infrastructure.Coordinator;
using Infrastructure.Coordinator.PubSub;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Tests.Coordinator;

public class IngressListenerTests : TestKit
{
    private static readonly string TestConfig = """
        akka.loglevel = "DEBUG"
        akka.actor.provider = "Akka.Cluster.ClusterActorRefProvider, Akka.Cluster"
        akka.scheduler.implementation = "Akka.TestKit.TestScheduler, Akka.TestKit"
        akka.cluster.pub-sub.name = "distributedPubSubMediator"
    """;

    private static Address Addr(int port = 5000, string system = "cluster-system", string host = "127.0.0.1")
        => new("akka.tcp", system, host, port);

    private readonly TestProbe _controllerProbe;
    private readonly IActorRef _listener;
    private readonly Mock<IRequiredActor<ClusterController>> _mockController;

    public IngressListenerTests(ITestOutputHelper output) : base(TestConfig, output)
    {
        _controllerProbe = CreateTestProbe();
        _mockController = new Mock<IRequiredActor<ClusterController>>();
        _mockController.SetupGet(x => x.ActorRef).Returns(_controllerProbe.Ref);

        _listener = Sys.ActorOf(Props.Create(() => new IngressListener(_mockController.Object)));

        // Basisklasse erwartet 2x SubscribeAck -> aktiviert danach die Receives
        var topic = "controller";
        var group = "group-controller";
        _listener.Tell(new SubscribeAck(new Subscribe(topic, _listener, group: null)));
        _listener.Tell(new SubscribeAck(new Subscribe(topic, _listener, group: group)));
    }

    private void FlushDebounce(TimeSpan? delay = null)
    {
        var d = delay ?? TimeSpan.FromMilliseconds(350); // > 200ms
        if (Sys.Scheduler is TestScheduler ts) ts.Advance(d);
        else System.Threading.Thread.Sleep(d);
    }

    private void ControllerAutoRespond(bool shardAvailable)
    {
        _controllerProbe.SetAutoPilot(new DelegateAutoPilot((sender, msg) =>
        {
            if (msg is IngressActivateRequest)
                sender.Tell(new IngressActivateResponse(shardAvailable), _controllerProbe);
            return AutoPilot.KeepRunning;
        }));
    }

    [Fact]
    public void Increase_then_Decrease_send_debounced_updates_to_controller()
    {
        var a = Addr(5100);
        // Increase -> debounced true
        EventFilter.Debug(start: "Debouncing shard count update").Expect(2, () =>
        {
            _listener.Tell(new IncreaseClusterMember(Addr()));
            _listener.Tell(new IncreaseClusterMember(Addr()));
        });

        FlushDebounce();

        var msg1 = _controllerProbe.ExpectMsg<IngressConnectionUpdateMessage>(TimeSpan.FromSeconds(1));
        Assert.True(msg1.IsConnected);

        // Decrease -> debounced false
        EventFilter.Debug(start: "Debouncing shard count update").Expect(2, () =>
        {
            _listener.Tell(new DecreaseClusterMember(Addr()));
            _listener.Tell(new DecreaseClusterMember(Addr()));
        });

        FlushDebounce();

        var msg2 = _controllerProbe.ExpectMsg<IngressConnectionUpdateMessage>(TimeSpan.FromSeconds(1));
        Assert.False(msg2.IsConnected);
    }

    [Fact]
    public void IngressConnectivityRequest_returns_false_when_no_local_ingress()
    {
        // Controller meldet ShardAvailable = true, aber lokal kein Ingress -> false
        ControllerAutoRespond(shardAvailable: true);

        _listener.Tell(IngressConnectivityRequest.Instance, TestActor);
        var res = ExpectMsg<IngressConnectivityResponse>(TimeSpan.FromSeconds(1));
        Assert.False(res.ShardAvailable);
    }

    [Fact]
    public void IngressConnectivityRequest_combines_local_and_controller_true_true_is_true()
    {
        // lokal: ein Ingress aktiv
        _listener.Tell(new IncreaseClusterMember(Addr(5300)));
        FlushDebounce(); // Debounce-Update an Controller ist hier egal

        // Controller: ShardAvailable = true
        ControllerAutoRespond(shardAvailable: true);

        _listener.Tell(IngressConnectivityRequest.Instance, TestActor);
        var res = ExpectMsg<IngressConnectivityResponse>(TimeSpan.FromSeconds(1));
        Assert.True(res.ShardAvailable);
    }

    [Fact]
    public void IngressConnectivityRequest_is_false_when_controller_reports_shard_unavailable()
    {
        // lokal: ein Ingress aktiv
        _listener.Tell(new IncreaseClusterMember(Addr(5400)));
        FlushDebounce();

        // Controller: ShardAvailable = false
        ControllerAutoRespond(shardAvailable: false);

        _listener.Tell(IngressConnectivityRequest.Instance, TestActor);
        var res = ExpectMsg<IngressConnectivityResponse>(TimeSpan.FromSeconds(1));
        Assert.False(res.ShardAvailable);
    }
}