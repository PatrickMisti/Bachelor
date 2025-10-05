using Akka.Actor;
using Akka.Cluster.Sharding;
using Akka.Event;
using Infrastructure.ShardRegion;

namespace Tests.ShardRegion.Assets;

internal class DriverTestActor : ReceiveActor
{
    private readonly ILoggingAdapter _log = Context.GetLogger();
    private DriverKey? _key;

    // keep payloads to send once a temporary child has terminated
    private readonly Dictionary<IActorRef, object> _pendingDeadletters = new();

    public DriverTestActor()
    {
        Become(Uninitialized);
    }

    protected override void PreStart() => _log.Info("DriverTestActor({0}) started", Self.Path.Name);
    protected override void PostStop() => _log.Info("DriverTestActor({0}) stopped", Self.Path.Name);

    private void Uninitialized()
    {
        Receive<CreateModelDriverMessage>(m =>
        {
            _key = m.Key;
            _log.Info("Initialized test entity {0}", _key);
            Sender.Tell(CreatedDriverMessage.Success(_key.Value));
            Become(Initialized);
        });

        Receive<StopEntity>(_ => Context.Stop(Self));

        // Any other message before init -> notify and passivate
        ReceiveAny(msg =>
        {
            var entityId = Self.Path.Name;
            _log.Warning("Received {0} before initialization for entity {1}. Passivating.", msg.GetType().Name, entityId);
            Sender.Tell(new NotInitializedMessage(entityId));
            Context.Parent.Tell(new Passivate(new StopEntity()));
        });
    }

    private void Initialized()
    {
        // test-only: trigger passivation via Coordinator
        Receive<TestPassivateMessage>(m =>
        {
            if (!KeysMatch(m.Key))
            {
                Sender.Tell(new NotInitializedMessage($"Wrong key for {_key}"));
                return;
            }
            Context.Parent.Tell(new Passivate(new StopEntity()));
        });

        // generic handler for normal shard messages
        Receive<IHasDriverId>(m =>
        {
            if (!KeysMatch(m.Key))
            {
                Sender.Tell(new NotInitializedMessage($"Wrong key for {_key}"));
                return;
            }

            // simple echo response to make assertions easy
            Sender.Tell(_key!.Value.ToString());
        });

        Receive<StopEntity>(_ => Context.Stop(Self));
    }

    private bool KeysMatch(DriverKey other)
        => _key is { } k && k.SessionId == other.SessionId && k.DriverNumber == other.DriverNumber;

    public static Props Prop() => Props.Create(() => new DriverTestActor());
}
internal sealed record TestPassivateMessage(DriverKey Key) : IHasDriverId;