namespace ClusterCoordinator.Actors.Messages;

public record IngressConnectionUpdateMessage(bool IsConnected) : IConnectionUpdateMessage;