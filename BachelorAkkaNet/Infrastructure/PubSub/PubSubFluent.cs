using Akka.Actor;
using Akka.Cluster.Tools.PublishSubscribe;

namespace Infrastructure.PubSub;

public sealed class PubSubFluent(IActorRef mediator, bool oneMessagePerGroup)
{
    public Publisher All => new(mediator, PubSubMember.All, true);
    public Publisher Backend => new(mediator, PubSubMember.Backend, oneMessagePerGroup);
    public Publisher Ingress => new(mediator, PubSubMember.Ingress, oneMessagePerGroup);
    public Publisher Api => new(mediator, PubSubMember.Api, oneMessagePerGroup);

    public Publisher Controller => new(mediator, PubSubMember.Controller, oneMessagePerGroup);

    public readonly struct Publisher(IActorRef mediator, PubSubMember topic, bool onePerGroup)
    {
        private string TopicName => topic.ToStr();

        public void Publish(IPubMessage message)
            => mediator.Tell(new Publish(TopicName, message, onePerGroup));
    }
}

public static class PubSubFluentExt
{
    public static PubSubFluent PubSub(this ActorSystem sys, bool oneMessagePerGroup = false)
        => new(DistributedPubSub.Get(sys).Mediator, oneMessagePerGroup);

    public static PubSubFluent PubSubGroup(this IActorContext ctx, bool oneMessagePerGroup = true)
        => ctx.System.PubSub(oneMessagePerGroup);
    public static PubSubFluent PubSubGroup(this ActorSystem ctx, bool oneMessagePerGroup = true)
        => ctx.PubSub(oneMessagePerGroup);

    public static PubSubFluent PubSub(this IActorContext ctx, bool oneMessagePerGroup = false) => ctx.System.PubSub(oneMessagePerGroup);
}