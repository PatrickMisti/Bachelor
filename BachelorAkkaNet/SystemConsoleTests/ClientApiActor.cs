using Akka.Actor;
using Akka.Event;
using Infrastructure.PubSub;
using Infrastructure.ShardRegion.Messages;

namespace SystemConsoleTests;

internal class ClientApiActor : ReceivePubSubActor<IPubSubTopicApi>
{
    public override void Activated()
    {
        var logger = Context.GetLogger();
        // Subscription is up
        Receive<NotifyDriverStateMessage>(msg =>
        {
            logger.Info($"Received message: {msg}");
            logger.Info($"DriverNo {msg.State!.Key.DriverNumber} with speed {msg.State.Speed}");
        });

        logger.Info("Create ClientApiActor");
    }

    public static Props Prop()
    {
        return Props.Create(() => new ClientApiActor());
    }
}