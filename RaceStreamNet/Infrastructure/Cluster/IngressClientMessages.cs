using Infrastructure.General.Message;

namespace Infrastructure.Cluster;

public sealed record IngressSessionRaceMessage(int SessionKey) : IPubMessage;