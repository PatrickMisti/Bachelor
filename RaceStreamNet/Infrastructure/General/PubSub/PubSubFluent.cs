using Akka.Actor;
using Akka.Cluster.Tools.PublishSubscribe;
using Infrastructure.General.Message;

namespace Infrastructure.General.PubSub;
public sealed class PubSubFluent(IActorRef mediator, bool oneMessagePerGroup)
{
    public Publisher All => new(mediator, PubSubMember.All, true);
    public Publisher Backend => new(mediator, PubSubMember.Backend, oneMessagePerGroup);
    public Publisher Ingress => new(mediator, PubSubMember.Ingress, oneMessagePerGroup);
    public Publisher Api => new(mediator, PubSubMember.Api, oneMessagePerGroup);

    public readonly struct Publisher(IActorRef mediator, PubSubMember topic, bool onePerGroup)
    {
        private string TopicName => topic.ToStr();
        private string TopicPath => $"/topic/{TopicName}";

        public void Publish(IPubMessage message)
            => mediator.Tell(new Publish(onePerGroup ? $"group-{TopicName}" : TopicName, message, onePerGroup));

        public async Task<T> Ask<T>(IPubMessage message)
            => await mediator.Ask<T>(new Send(TopicPath, message));

        public async Task<T> AskAll<T>(IPubMessage message)
            => await mediator.Ask<T>(new SendToAll(TopicPath, message));

        public void PublishAll(IPubMessage message) 
            => mediator.Tell(new SendToAll(TopicPath, message));
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
