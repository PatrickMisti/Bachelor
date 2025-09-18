using Akka.Actor;
using Akka.Event;
using Akka.Hosting;
using DriverTelemetryIngress.Actors.Messages;
using Infrastructure.Coordinator;
using Infrastructure.Coordinator.PubSub;
using Infrastructure.General.PubSub;
using Infrastructure.Shard;

namespace DriverTelemetryIngress.Actors;

public class ControllerHandlerActor : ReceivePubSubActor<IPubSubTopicIngress>
{
    private readonly ILoggingAdapter _log = Context.GetLogger();
    private IActorRef _controller;

    public ControllerHandlerActor(IRequiredActor<ClusterCoordinatorMarker> controller)
    {
        _controller = controller.ActorRef;
    }

    public override void Activated()
    {
        Receive<NotifyIngressShardIsOnline>(msg =>
        {
            _log.Info("Got notification to shard region online//offline from controller!");
        });

        ReceiveAsync<ShardConnectionAvailableRequest>(async _ =>
        {
            var res = await _controller.Ask<IngressConnectivityResponse>(IngressConnectivityRequest.Instance);
            _log.Info($"Got response from controller with shard is online: {res.ShardAvailable}");
            Sender.Tell(new ShardConnectionAvailableResponse(res.ShardAvailable));
        });
    }
}