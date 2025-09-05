using Akka.Actor;
using Infrastructure.General.PubSub;
using Infrastructure.Shard.Messages.Notification;
using Infrastructure.Shard.Messages.RequestMessages;
using Infrastructure.Shard.Messages.ResponseMessage;

namespace IntegrationTests.ShardRegion.DemoActors;

public class DemoApiActor : ReceivePubSubActor<IPubSubTopicApi>
{
    public DemoApiActor(IActorRef probe)
    {
        Receive<NotifyDriverStateMessage>(msg =>
        {
            // Handle the API message
            // For example, you can log it or process it
            Console.WriteLine($"Received API message: {msg.Key.ToString()}");
            probe.Tell(msg);
        });

        Receive<ResponseHolder>(msg =>
        {
            // Forward the response to the probe for testing purposes
            probe.Tell(msg.Response);
        });

        Receive<GetDriverStateRequest>(MakeRequestTest);

        Receive<GetDriverStateResponse>(m =>
        {
            probe.Tell(m);
        });
    }

    public void MakeRequestTest(GetDriverStateRequest req) =>
        Context.PubSub().Backend.Publish(req);
    public static Props Props(IActorRef probe) => Akka.Actor.Props.Create(() => new DemoApiActor(probe));
}

internal record ResponseHolder(GetDriverStateResponse? Response);