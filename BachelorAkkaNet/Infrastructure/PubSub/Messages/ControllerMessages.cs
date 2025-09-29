namespace Infrastructure.PubSub.Messages;

public sealed record NodeInClusterRequest() : IPubMessage
{
    public static NodeInClusterRequest Instance { get; } = new();
}

public record NodeInClusterResponse(int IsInCluster) : IPubMessage;