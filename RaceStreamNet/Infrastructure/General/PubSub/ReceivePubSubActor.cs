using Akka.Actor;
using Akka.Cluster.Tools.PublishSubscribe;
using Akka.Event;

namespace Infrastructure.General.PubSub;

public class ReceivePubSubActor<TTopic> : ReceiveActor where TTopic : IPubSubTopic
{
    private IActorRef _pubSubActorRef = ActorRefs.Nobody;
    private readonly ILoggingAdapter _log = Context.GetLogger();
    private PubSubMember _member;

    protected override void PreStart()
    {
        _pubSubActorRef = DistributedPubSub.Get(Context.System).Mediator;
        if (_pubSubActorRef.IsNobody())
            throw new ActorNotFoundException("Mediator not connected!");

        _member = PubSubTypeMapping.ToMember(typeof(TTopic)) ?? PubSubMember.All;
        _log.Info("Mediator is connected to cluster");

        _pubSubActorRef.Tell(new Subscribe(_member.ToStr(), Self));
        if (_member is not PubSubMember.All)
            _pubSubActorRef.Tell(new Subscribe(PubSubMember.All.ToStr(), Self));
    }

    protected override void PostStop()
    {
        if (_pubSubActorRef.IsNobody()) return;

        _pubSubActorRef.Tell(new Unsubscribe(_member.ToStr(), Self));
        if (_member is not PubSubMember.All)
            _pubSubActorRef.Tell(new Unsubscribe(PubSubMember.All.ToStr(), Self));
    }
}

public enum PubSubMember
{
    All,
    Backend,
    Ingress,
    Api
}