
using Akka.Actor;
using Akka.Event;
using Akka.Hosting;
using Infrastructure.General.PubSub;
using Infrastructure.Shard;
using Infrastructure.Shard.Messages;
using Infrastructure.Shard.Messages.RequestMessages;
using Infrastructure.Shard.Messages.ResponseMessage;
using Infrastructure.Shard.Models;

namespace DriverTelemetryIngress.Actors;

public class ShardRegionProxy : ReceiveActor
{
    private IActorRef _proxy;
    private readonly ILoggingAdapter _logger = Context.GetLogger();

    public ShardRegionProxy(IRequiredActor<DriverRegionMarker> proxy)
    {
        _proxy = proxy.ActorRef;
        _logger.Info("ShardRegionProxy started");

        Receive<GetDriverStateResponse>(msg =>
        {
            _logger.Info($"Hawara GetDriverResponse {msg.Key} {msg.DriverState.Speed}");
        });

        Receive<UpdateTelemetryMessage>(msg =>
        {
            _proxy.Forward(msg);
        });

        Receive<object>(msg =>
        {
            _logger.Info($"Got Message CreatedDriverMessage with key: {msg.GetType()}");

            if (msg is CreateModelDriverMessage createMsg)
            {
                //_proxy.Ask<object>(new UpdateIntervalMessage(createMsg.Key,0.2,DateTime.Now)).PipeTo(Self, success: s => s, failure: f => f);
                _proxy.Ask<object>(createMsg).PipeTo(Self, success: s => s, failure: f => f);
                _logger.Info($"state key is not null anymore {createMsg.Key}");
            }
            else if (msg is Status.Failure errorMsg)
            {
                _logger.Info($"Error was send: {errorMsg.Cause}");
            }
        });

        
        _logger.Info("Send Self CreateModelDriverMessage");
        //Context.System.PubSubGroup().Backend.Publish(new GetDriverStateRequest(DriverKey.Create(2222,15)!));

    }
}