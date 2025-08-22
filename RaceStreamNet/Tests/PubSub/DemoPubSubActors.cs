using Akka.Actor;
using Infrastructure.General.Message;
using Infrastructure.General.PubSub;

namespace Tests.PubSub;

public sealed class BackendSubscriberTest : ReceivePubSubActor<IPubSubTopicBackend>
{
    public BackendSubscriberTest(IActorRef probe)
    {
        // Acks an Probe
        //Receive<SubscribeAck>(ack => probe.Tell(("ack", ((Subscribe)ack.Subscribe).Topic)));
        // Payloads an Probe
        ReceiveAny(msg => probe.Tell(("msg back", msg)));
    }
}

public sealed class AllSubscriberTest : ReceivePubSubActor<IPubSubTopicAll>
{
    public AllSubscriberTest(IActorRef probe)
    {
        //Receive<SubscribeAck>(ack => probe.Tell(("ack", ((Subscribe)ack.Subscribe).Topic)));
        ReceiveAny(msg => probe.Tell(("msg all", msg)));
    }
}
public class MessageTest : IPubMessage
{
    public string Message { get; set; } = string.Empty;
}
