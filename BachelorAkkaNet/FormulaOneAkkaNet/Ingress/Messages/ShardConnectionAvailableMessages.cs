namespace FormulaOneAkkaNet.Ingress.Messages;

public record ShardConnectionAvailableRequest
{
    public static ShardConnectionAvailableRequest Instance { get; } = new();

    private ShardConnectionAvailableRequest() { }
}

public record ShardConnectionAvailableResponse(bool IsOnline);
