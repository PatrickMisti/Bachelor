namespace ClusterCoordinator.Actors.Messages.Request;

public record ShardConnectionRequest
{
    public static ShardConnectionRequest Instance => new ();

    private ShardConnectionRequest()
    {
    }
}