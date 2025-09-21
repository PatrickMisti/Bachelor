using Akka.Actor;
using Akka.Event;
using Akka.Hosting;
using DriverTelemetryIngress.Bridge;
using Infrastructure.Shard;

namespace DriverTelemetryIngress.Actors;


// not use only testing
public class ShardRegionProxy : ReceiveActor
{
    private IActorRef _proxy = ActorRefs.Nobody;
    private readonly ILoggingAdapter _logger = Context.GetLogger();

    public ShardRegionProxy()
    {
        _logger.Info("ShardRegionProxy started");

        Receive<IOpenF1Dto>(msg =>
        {
            _logger.Info("New Message to ShardRegion will be send!");
            _proxy.Forward(msg);
        });
    }

    protected override void PreStart()
    {
        _proxy = ActorRegistry.For(Context.System).Get<DriverRegionMarker>();
        _logger.Info("ShardRegionProxy ready");
        base.PreStart();
    }
}