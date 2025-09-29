namespace Client.Benchmark.Actors.Messages;

public class AskForNodesInClusterRequest
{
    public static AskForNodesInClusterRequest Instance { get; } = new();
    private AskForNodesInClusterRequest() { }
}

public record AskForNodesInClusterResponse(int Nodes);
