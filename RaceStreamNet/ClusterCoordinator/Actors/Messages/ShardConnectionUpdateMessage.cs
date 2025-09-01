namespace ClusterCoordinator.Actors.Messages;

public record ShardConnectionUpdateMessage(bool IsShardOnline) : IConnectionUpdateMessage;