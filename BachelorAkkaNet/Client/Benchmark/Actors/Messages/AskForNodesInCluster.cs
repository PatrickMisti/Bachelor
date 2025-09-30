namespace Client.Benchmark.Actors.Messages;

internal class AskForNodesInClusterRequest
{
    public static AskForNodesInClusterRequest Instance { get; } = new();
    private AskForNodesInClusterRequest() { }
}

internal record AskForNodesInClusterResponse(int Nodes);
