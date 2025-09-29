namespace FormulaOneAkkaNet.Coordinator.Broadcasts;

public record ShardConnectionRequest
{
    public static ShardConnectionRequest Instance => new();

    private ShardConnectionRequest() { }
}

public record ShardCountRequest
{
    public static ShardCountRequest Instance => new();

    private ShardCountRequest() { }
}

public record IngressCountRequest
{
    public static IngressCountRequest Instance => new();

    private IngressCountRequest() { }
}