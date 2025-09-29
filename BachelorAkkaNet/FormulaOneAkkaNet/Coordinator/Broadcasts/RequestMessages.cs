namespace FormulaOneAkkaNet.Coordinator.Broadcasts;

public record ShardConnectionRequest
{
    public static ShardConnectionRequest Instance => new();

    private ShardConnectionRequest() { }
}