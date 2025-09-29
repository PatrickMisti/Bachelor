namespace Infrastructure.PubSub.Messages;

public record IngressConnectivityRequest() : IPubMessage
{
    public static IngressConnectivityRequest Instance => new();
}

public record IngressConnectivityResponse(bool ShardAvailable) : IPubMessage;

public record NotifyIngressShardIsOnline(bool IsOnline) : IPubMessage;