using Akka.Actor;
using Akka.Event;
using Akka.TestKit;
using Infrastructure.Coordinator;
using Infrastructure.Coordinator.PubSub;
using Infrastructure.General.PubSub;

namespace SystemTests;

public class IngressGhostActor : ReceivePubSubActor<IPubSubTopicIngress>
{

    public override void Activated()
    {
        var logger = Context.GetLogger();
        logger.Info("init test actor");
        Receive<NotifyIngressShardIsOnline>(msg =>
        {
            logger.Info($"Notify Ingress because shard is online {msg.IsOnline}");
        });

        Receive<TestGetInfoOfShard>(_ =>
        {
            logger.Info("Get Info of Shard");
            Context.PubSub().Controller.Publish(IngressConnectivityRequest.Instance);
        });

        Receive<IngressConnectivityResponse>(msg =>
        {
            logger.Info($"Get Ingress Response is online : {msg.ShardAvailable}");
        });

        logger.Info("Notify request");
        Context.PubSub().Controller.Publish(IngressConnectivityRequest.Instance);
    }
}

public record IsStarted();

public record TestGetInfoOfShard
{
    public static TestGetInfoOfShard Instance => new();

    private TestGetInfoOfShard() { }
}