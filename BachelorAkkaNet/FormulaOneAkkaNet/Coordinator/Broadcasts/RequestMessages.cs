namespace FormulaOneAkkaNet.Coordinator.Broadcasts;

public record ShardConnectionRequest
{
    public static ShardConnectionRequest Instance => new();
}

public record ShardCountRequest
{
    public static ShardCountRequest Instance => new();
}

public record IngressCountRequest
{
    public static IngressCountRequest Instance => new();
}