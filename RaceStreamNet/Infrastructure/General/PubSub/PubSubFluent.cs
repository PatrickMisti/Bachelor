using Akka.Actor;
using Akka.Cluster.Tools.PublishSubscribe;
using Infrastructure.General.Message;

namespace Infrastructure.General.PubSub;
public sealed class PubSubFluent(IActorRef mediator)
{
    public Publisher All => new(mediator, PubSubMember.All, true);
    public Publisher Backend => new(mediator, PubSubMember.Backend);
    public Publisher Ingress => new(mediator, PubSubMember.Ingress);
    public Publisher Api => new(mediator, PubSubMember.Api);

    public readonly struct Publisher(IActorRef mediator, PubSubMember topic, bool onePerGroup = false)
    {
        private string TopicName => topic.ToStr();
        private string TopicPath => $"/topic/{TopicName}";

        public void Publish(IPubMessage message)
        {
            mediator.Tell(new Publish(TopicName, message));
        }

        public async Task<T> Ask<T>(IPubMessage message)
            => await mediator.Ask<T>(new Send(TopicPath, message));

        public async Task<T> AskAll<T>(IPubMessage message)
            => await mediator.Ask<T>(new SendToAll(TopicPath, message));
    }
}

public static class PubSubFluentExt
{
    public static PubSubFluent PubSub(this ActorSystem sys)
        => new(DistributedPubSub.Get(sys).Mediator);

    public static PubSubFluent PubSub(this IActorContext ctx) => ctx.System.PubSub();
}
