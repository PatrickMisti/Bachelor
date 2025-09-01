namespace ClusterCoordinator.Actors.Messages.Request;

public record IngressActivateRequest
{
    public static IngressActivateRequest Instance => new ();

    private IngressActivateRequest() { }
}