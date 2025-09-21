namespace FormulaOneAkkaNet.Coordinator.Broadcasts;

public record IngressActivateRequest
{
    public static IngressActivateRequest Instance => new();

    private IngressActivateRequest() { }
}

public record ShardConnectionRequest
{
    public static ShardConnectionRequest Instance => new();

    private ShardConnectionRequest() { }
}