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

        ReceiveAsync<IOpenF1Dto>(async msg =>
        {
            try
            {
                var message = msg switch
                {
                    IntervalDriverDto d => proxy.Ask<Status>(d.ToMap()),
                    PersonalDriverDataDto d => proxy.Ask<Status>(d.ToMap()),
                    PositionOnTrackDto d => proxy.Ask<Status>(d.ToMap()),
                    TelemetryDateDto d => proxy.Ask<Status>(d.ToMap()),
                    _ => null
                };
                /*switch (msg)
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
                }*/

                if (message == null)
                {
                    _log.Warning($"Message was no IOpenF1Dto message {msg.GetType()}");
                    // Ack for backpressure
                    Sender.Tell(StreamAck.Instance);
                    return;
                }

                _log.Debug($"Forwarding message {msg.GetType()} to proxy");
                Task.WaitAll(message);
                var result = await message;

                if (result is Status.Success)
                    _log.Debug("Send update to shard region");

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
