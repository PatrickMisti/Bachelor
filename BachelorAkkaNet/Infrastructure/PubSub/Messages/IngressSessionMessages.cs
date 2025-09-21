namespace Infrastructure.PubSub.Messages;

public sealed record IngressSessionRaceMessage(int SessionKey) : IPubMessage;