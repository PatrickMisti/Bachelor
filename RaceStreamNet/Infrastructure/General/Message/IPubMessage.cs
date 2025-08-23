using Akka.Actor;

namespace Infrastructure.General.Message;

public interface IPubMessage
{
}

public interface IPubMessageRequest : IPubMessage
{
    IActorRef ActorRef { get; init; }
}