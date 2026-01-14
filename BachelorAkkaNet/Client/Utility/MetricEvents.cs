using Infrastructure.ShardRegion.Messages;

namespace Client.Utility;

public interface IMetricsPublisher
{
    void Publish(IMetricEvent sample);
}

public interface IMetricEvent { DateTime TimestampUtc { get; } }

public readonly record struct ReqEnd(double LatencyMs, bool Success, int Messages) : IMetricEvent
{
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
}

public record StreamStarted : IMetricEvent
{
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
}

public readonly record struct StreamBatch(int Count, double LatencyMs, bool Success) : IMetricEvent
{
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
}

public readonly record struct StreamEnded(bool Success) : IMetricEvent
{
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
}

public readonly record struct PipelineMode(string Mode) : IMetricEvent
{
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
}

// Freier Haken für spätere Erweiterungen (Cluster-Infos etc.)
public readonly record struct CustomMetric(string Name, double Value) : IMetricEvent
{
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
}

public record DriverInfoState(NotifyDriverStateMessage Msg) : IMetricEvent
{
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
}