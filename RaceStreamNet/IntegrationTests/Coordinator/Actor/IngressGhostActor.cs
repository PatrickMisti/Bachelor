using Akka.Actor;
using Akka.TestKit;
using Infrastructure.Coordinator;
using Infrastructure.Coordinator.PubSub;
using Infrastructure.General.PubSub;
using Xunit.Abstractions;

namespace IntegrationTests.Coordinator.Actor;

public class IngressGhostActor: ReceivePubSubActor<IPubSubTopicIngress>
{
    public TestProbe Probe { get; set; }
    public ITestOutputHelper _output;

    public IngressGhostActor(TestProbe probe, ITestOutputHelper output)
    {
        Probe = probe;
        _output = output;
    }

    public override void Activated()
    {
        _output.WriteLine("init test actor");
        Receive<NotifyIngressShardIsOnline>(msg =>
        {
            _output.WriteLine($"Notify Ingress because shard is online {msg.IsOnline}");
            Probe.Ref.Tell(msg);
        });

        Receive<TestGetInfoOfShard>(_ =>
        {
            //_output.WriteLine("Get Info of Shard");
            Context.PubSub().Controller.Publish(IngressConnectivityRequest.Instance);
        });

        Receive<IngressConnectivityResponse>(msg =>
        {
            _output.WriteLine($"Get Ingress Response is online : {msg.ShardAvailable}");
            Probe.Ref.Tell(msg);
        });
    }
}

public record TestGetInfoOfShard
{
    public static TestGetInfoOfShard Instance => new();

    private TestGetInfoOfShard() {}
}