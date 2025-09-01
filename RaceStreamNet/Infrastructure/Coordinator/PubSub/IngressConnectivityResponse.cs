using Infrastructure.General.Message;

namespace Infrastructure.Coordinator.PubSub;

public record IngressConnectivityResponse(bool ShardAvailable) : IPubMessage;