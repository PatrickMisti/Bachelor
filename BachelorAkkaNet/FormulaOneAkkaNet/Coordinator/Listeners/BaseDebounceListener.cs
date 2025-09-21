using Akka.Actor;
using Akka.Event;
using FormulaOneAkkaNet.Coordinator.Messages;
using Infrastructure.PubSub;

namespace FormulaOneAkkaNet.Coordinator.Listeners;

public class BaseDebounceListener(IActorRef controller) : ReceivePubSubActor<IPubSubTopicController>
{
    protected readonly ILoggingAdapter Logger = Context.GetLogger();
    protected readonly IActorRef Controller = controller;
    private ICancelable? _debounceTask;


    protected void SendCountUpdate(IConnectionUpdateMessage message) => SendCountUpdateDebounced(message);


    // Send the current count of active backends to the controller
    private void SendCountUpdateWithoutDebounced(IConnectionUpdateMessage message)
    {
        Logger.Debug("Sending shard count update: {0}", message.ToString());
        Controller.Tell(message);
    }

    // debounced version to avoid flooding the controller with updates
    private void SendCountUpdateDebounced(IConnectionUpdateMessage message)
    {
        Logger.Debug("Debouncing shard count update");
        // Cancel any existing debounce task
        _debounceTask?.Cancel();
        // Schedule a new debounce task
        _debounceTask = Context.System.Scheduler.ScheduleTellOnceCancelable(
            delay: TimeSpan.FromMilliseconds(200),
            receiver: Controller,
            message: message,
            sender: Self
        );
    }
}