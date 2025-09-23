namespace Infrastructure.General;

/// <summary>
/// Message to initiate the stream.
/// </summary>
public sealed record StreamInit
{
    public static readonly StreamInit Instance = new();
    private StreamInit() { }
}
public sealed record StreamAck
{
    public static readonly StreamAck Instance = new();
    private StreamAck() { }
}

public sealed record StreamCompleted
{
    public static readonly StreamCompleted Instance = new();
    private StreamCompleted() { }
}