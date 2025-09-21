using Akka.Actor;
using Akka.Event;
using FormulaOneAkkaNet.Coordinator.Broadcasts;
using FormulaOneAkkaNet.Coordinator.Messages;

namespace FormulaOneAkkaNet.Coordinator.Listeners;

public class ShardListener(IActorRef controller) : BaseDebounceListener(controller)
{
    private readonly HashSet<Address> _activeBackends = new();

    public override void Activated()
    {
        Logger.Info("Activate Handlers in ShardListener!");
        HandleClusterEvents();
    }

    private void HandleClusterEvents()
    {
        Receive<IncreaseClusterMember>(msg =>
        {
            Logger.Debug("Increase counter");
            _activeBackends.Add(msg.ClusterMemberRef);
            SendCountUpdate(new ShardConnectionUpdateMessage(_activeBackends.Count > 0));
        });

        Receive<DecreaseClusterMember>(msg =>
        {
            Logger.Debug("Decrease counter");
            _activeBackends.Remove(msg.ClusterMemberRef);
            SendCountUpdate(new ShardConnectionUpdateMessage(_activeBackends.Count > 0));
        });

        Receive<ShardConnectionRequest>(msg =>
            Sender.Tell(new ShardConnectionUpdateMessage(_activeBackends.Count > 0)));
    }
}
