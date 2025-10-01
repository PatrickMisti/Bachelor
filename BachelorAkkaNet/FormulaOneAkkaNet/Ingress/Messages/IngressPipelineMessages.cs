using Infrastructure.General;

namespace FormulaOneAkkaNet.Ingress.Messages;

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

public sealed record PipelineModeRequest()
{
    public static PipelineModeRequest Instance => new();
}

public sealed record PipelineModeResponse(Mode PipelineMode);

public sealed record PipelineModeChangeRequest(Mode NewPipelineMode);

public sealed record PipelineModeChangeResponse(Mode PipelineMode);
