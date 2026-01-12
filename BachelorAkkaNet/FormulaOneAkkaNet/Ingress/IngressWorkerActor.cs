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
            var replyTo = Sender;
            try
            {
                var task = msg switch
                {
                    IntervalDriverDto d => proxy.Ask<Status>(d.ToMap(), TimeSpan.FromSeconds(8)),
                    PersonalDriverDataDto d => proxy.Ask<Status>(d.ToMap(), TimeSpan.FromSeconds(8)),
                    PositionOnTrackDto d => proxy.Ask<Status>(d.ToMap(), TimeSpan.FromSeconds(8)),
                    TelemetryDateDto d => proxy.Ask<Status>(d.ToMap(), TimeSpan.FromSeconds(8)),
                    _ => null
                };

                if (task is null)
                {
                    _log.Warning($"Message was no IOpenF1Dto message {msg.GetType()}");
                    // Ack for backpressure
                    replyTo.Tell(StreamAck.Instance);
                    return;
                }

                _log.Debug($"Forwarding message {msg.GetType()} to proxy");
                var result = await task.ConfigureAwait(false);

                if (result is Status.Success)
                    _log.Debug("Send update to shard region");

                // Ack for backpressure
                replyTo.Tell(StreamAck.Instance);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Worker mapping/forwarding failed");
                // send ack even on error to avoid stream deadlock
                replyTo.Tell(StreamAck.Instance);
            }
        });
    }

    public static Props Prop(IActorRef shardProxy) => Props.Create(() => new IngressWorkerActor(shardProxy));
}
