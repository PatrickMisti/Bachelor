using Akka.Actor;
using Akka.Event;
using Infrastructure.General;
using Infrastructure.Http;

namespace FormulaOneAkkaNet.Ingress;

public class IngressWorkerActor : ReceiveActor
{
    private readonly ILoggingAdapter _log = Context.GetLogger();

    public IngressWorkerActor(IActorRef shardProxy)
    {
        var proxy = shardProxy;

        // Acking for Sink.ActorRefWithAck
        Receive<StreamInit>(_ => Sender.Tell(StreamAck.Instance));
        Receive<StreamCompleted>(_ => Context.Stop(Self));

        Receive<IOpenF1Dto>(msg =>
        {
            try
            {
                switch (msg)
                {
                    case IntervalDriverDto d:
                        proxy.Forward(d.ToMap());
                        break;
                    case PersonalDriverDataDto d:
                        proxy.Forward(d.ToMap());
                        break;
                    case PositionOnTrackDto d:
                        proxy.Forward(d.ToMap());
                        break;
                    case TelemetryDateDto d:
                        proxy.Forward(d.ToMap());
                        break;
                    default:
                        _log.Warning($"Message was no IOpenF1Dto message {msg.GetType()}");
                        break;
                }

                _log.Debug($"Forwarding message {msg.GetType()} to proxy");
                // an ShardRegion (Proxy) forwarden
                ;

                // Ack for backpressure
                Sender.Tell(StreamAck.Instance);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Worker mapping/forwarding failed");
                // send ack even on error to avoid stream deadlock
                Sender.Tell(StreamAck.Instance);
            }
        });
    }

    public static Props Prop(IActorRef shardProxy) => Props.Create(() => new IngressWorkerActor(shardProxy));
}
