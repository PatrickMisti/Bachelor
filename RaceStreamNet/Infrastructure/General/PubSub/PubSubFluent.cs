using Akka.Actor;
using Akka.Cluster.Tools.PublishSubscribe;

namespace Infrastructure.General.PubSub;
public sealed class PubSubFluent(IActorRef mediator)
{
    public Publisher All => new(mediator, PubSubMember.All);
    public Publisher Backend => new(mediator, PubSubMember.Backend);
    public Publisher Ingress => new(mediator, PubSubMember.Ingress);
    public Publisher Api => new(mediator, PubSubMember.Api);

    public readonly struct Publisher(IActorRef mediator, PubSubMember topic)
    {
        public void Publish(object message, bool onePerGroup = false)
            => mediator.Tell(new Publish(topic.ToStr(), message, onePerGroup));
    }
}

public static class PubSubFluentExt
{
    public static PubSubFluent PubSub(this ActorSystem sys)
        => new(DistributedPubSub.Get(sys).Mediator);

    public static PubSubFluent PubSub(this IActorContext ctx) => ctx.System.PubSub();
}
