using Akka.Actor;
using Akka.Event;
using Client.Benchmark.Actors.Messages;
using Client.Utility;
using Infrastructure.Http;
using Infrastructure.PubSub;
using Infrastructure.ShardRegion.Messages;
using System.Diagnostics;

namespace Client.Benchmark.Actors;

public class RegionNotifyOnceSessionActor : ReceivePubSubActor<IPubSubTopicApi>
{
    private ILoggingAdapter Logger => Context.GetLogger();

    private readonly IActorRef _replyTo;
    private readonly IActorRef _ingress;
    private readonly IMetricsPublisher _metrics;

    private readonly int _sessionKey;
    private bool _isCreated = false;
    private Stopwatch? _sw;

    public RegionNotifyOnceSessionActor(IActorRef replyTo, IActorRef ingress, IMetricsPublisher metrics, int sessionKey)
    {
        _replyTo = replyTo;
        _ingress = ingress;
        _metrics = metrics;
        _sessionKey = sessionKey;

        Logger.Info("Started shard region test");

    }

    public override void Activated()
    {
        Receive<NotifyDriverStateMessage>(msg =>
        {
            _metrics.Publish(new StreamBatch(1, _sw!.ElapsedMilliseconds, true));
            _metrics.Publish(new DriverInfoState(msg));
        });

        // Upstream meldet Failure explizit
        Receive<NotifyStatusFailureMessage>(f =>
        {
            _metrics.Publish(new StreamBatch(1, _sw!.ElapsedMilliseconds, false));
        });

        Receive<ReceiveTimeout>(_ =>
        {
            //_metrics.Publish(new StreamBatch(1, _sw!.ElapsedMilliseconds, false));
            _metrics.Publish(new StreamEnded(false));
            CleanupAndStop();
        });

        Logger.Info("Start Fire and Forget");
        _sw = Stopwatch.StartNew();
        _metrics.Publish(new StreamStarted());
        _ingress.Tell(new HttpStartRaceSessionWithRegionMessage(_sessionKey));
        Context.SetReceiveTimeout(TimeSpan.FromSeconds(10));
    }

    private void CleanupAndStop()
    {
        _sw?.Stop();
        Context.SetReceiveTimeout(null);
        Context.Stop(Self);
    }

    public static Props Props(IActorRef replyTo, IActorRef ingress, IMetricsPublisher metrics, int sessionKey) =>
        Akka.Actor.Props.Create(() => new RegionNotifyOnceSessionActor(replyTo, ingress, metrics, sessionKey));
}