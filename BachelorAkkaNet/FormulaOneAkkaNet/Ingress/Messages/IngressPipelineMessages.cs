namespace FormulaOneAkkaNet.Ingress.Messages;

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

/// <summary>
/// to switch between push and polling mode
/// </summary>
public sealed record UsePushStream
{
    public static readonly UsePushStream Instance = new();

    private UsePushStream() { }
}

public sealed record UsePollingStream
{
    public TimeSpan Interval { get; private set; } = TimeSpan.FromSeconds(4);
    public static readonly UsePollingStream Instance = new();

    public static UsePollingStream InstanceWithTimeSpan(TimeSpan interval) => new UsePollingStream { Interval = interval };
    private UsePollingStream() { }
}

public sealed record StopPipeline
{
    public static readonly StopPipeline Instance = new();
    private StopPipeline() { }
}

public enum Mode { None, Push, Polling }
