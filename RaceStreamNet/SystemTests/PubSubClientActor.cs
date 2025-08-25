using System.Diagnostics;
using Akka.Actor;
using Akka.Cluster.Tools.PublishSubscribe;
using Akka.Event;
using Akka.Streams.Actors;
using Infrastructure.Cluster.Messages.RequestMessages;
using Infrastructure.Cluster.Messages.ResponseMessage;
using Infrastructure.General.PubSub;

namespace SystemTests;

public sealed class PubSubClientActor : ReceiveActor
{
    private readonly ILoggingAdapter _log = Context.GetLogger();
    private readonly string _driverId;
    private IActorRef _mediator = ActorRefs.Nobody;
    Stopwatch _stopwatch = Stopwatch.StartNew();

    public PubSubClientActor(string driverId)
    {
        _driverId = driverId;

        Receive<GetDriverStateResponse>(res =>
        {
            //_log.Info("Received GetDriverStateResponse for {0}: {1}", res.DriverId, res.DriverState.ToString() ?? "(null)");
            Console.WriteLine($"[OK] Driver={res.DriverId}");
            _stopwatch.Stop();
            Console.WriteLine($"Stopwatch says{_stopwatch.ElapsedMilliseconds}ms");
            // Nicht sofort beenden – ein paar Sekunden „am Leben bleiben“, damit der Service uns nicht als flap sieht.
            //Context.System.Scheduler.ScheduleTellOnce(TimeSpan.FromSeconds(2), Self, PoisonPill.Instance, ActorRefs.NoSender);
        });

        ReceiveAny(msg =>
        {
            _log.Warning("Unexpected message: {0}", msg);
        });

        
    }

    private async Task PublishWithRetry( GetDriverStateRequest msg,
        int maxAttempts = 3, int delayMs = 500)
    {
        for (var i = 1; i <= maxAttempts; i++)
        {
            Context.PubSub().Backend.Publish(msg);
            // kleine Pause – Registrierung/Cluster-Gossip braucht Zeit
            await Task.Delay(delayMs);
        }
    }

    protected override void PreStart()
    {
        //PublishWithRetry(new GetDriverStateRequest("VER"));
        //worked
        //
        //
        var request = new GetDriverStateRequest(_driverId);
        /*_mediator = DistributedPubSub.Get(Context.System).Mediator;

        // Publish: Sender = Self, damit die Antwort an uns zurückkommt

        _log.Info("Publishing GetDriverStateRequest({0}) to topic 'backend'", _driverId);
        _mediator.Tell(new Publish("backend", request), Self);*/
        _stopwatch.Start();
        Context.System.PubSub(true).Backend.Publish(request);
    }
}
