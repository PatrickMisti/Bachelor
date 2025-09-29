namespace Infrastructure.PubSub.Messages;

public record NodeInClusterRequest : IPubMessage
{
    public static NodeInClusterRequest Instance => new();

    private NodeInClusterRequest() { }
}

public record NodeInClusterResponse(int IsInCluster) : IPubMessage;