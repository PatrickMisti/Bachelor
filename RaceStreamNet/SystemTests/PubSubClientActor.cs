using System.Diagnostics;
using Akka.Actor;
using Akka.Event;
using Infrastructure.General.PubSub;
using Infrastructure.Shard.Messages.RequestMessages;
using Infrastructure.Shard.Messages.ResponseMessage;
using Infrastructure.Shard.Models;

namespace SystemTests;

public sealed class PubSubClientActor : ReceiveActor
{
    private readonly ILoggingAdapter _logger = Context.GetLogger();
    private readonly DriverKey _driverId;
    private IActorRef _mediator = ActorRefs.Nobody;
    Stopwatch _stopwatch = Stopwatch.StartNew();

    public PubSubClientActor(DriverKey driverId)
    {
        _driverId = driverId;

        Receive<GetDriverStateResponse>(res =>
        {
            //_logger.Info("Received GetDriverStateResponse for {0}: {1}", res.DriverId, res.DriverState.ToString() ?? "(null)");
            Console.WriteLine($"[OK] Driver={res.Key}");
            //_stopwatch.Stop();
            //Console.WriteLine($"Stopwatch says{_stopwatch.ElapsedMilliseconds}ms");
            // Nicht sofort beenden – ein paar Sekunden „am Leben bleiben“, damit der Service uns nicht als flap sieht.
            //Context.System.Scheduler.ScheduleTellOnce(TimeSpan.FromSeconds(2), Self, PoisonPill.Instance, ActorRefs.NoSender);
        });

        ReceiveAny(msg =>
        {
            _logger.Info("Unexpected message: {0}", msg);
        });

        /*Receive<TestGetDriverState>(msg =>
        {
            _logger.Warning($"Got message {msg.Response.DriverId}");
        });*/

        var request = new GetDriverStateRequest(_driverId);
        _logger.Info("Start Test to ask Handler!");
        Context.System.PubSubGroup().Backend.Publish(request);
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

        _logger.Info("Publishing GetDriverStateRequest({0}) to topic 'backend'", _driverId);
        _mediator.Tell(new Publish("backend", request), Self);*/
        
        //_stopwatch.Start();
       
    }

    public class TestGetDriverState(GetDriverStateResponse response)
    {
        public GetDriverStateResponse Response { get; } = response;
    }
}
