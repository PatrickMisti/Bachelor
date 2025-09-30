using System.Collections.Concurrent;

namespace Client.Utility;

public record ClusterSnapshot(int Nodes, int Shards, int Entities, Dictionary<string, int> ShardDistribution, string RebalanceStatus);


public record MetricsUpdate(
    double ThroughputPerSec,
    IReadOnlyList<double> Latencies,
    double ErrorPercent,
    int Messages,
    TimeSpan RunningFor,
    ClusterSnapshot? Cluster
);


public record ClusterEvent(string Type, DateTime Timestamp);

public sealed record MetricsSample(double LatencyMs, bool Success, int Messages = 1);

public interface IMetricsSink
{
    void Publish(MetricsSample sample);
}

public class MetricsSnapshot
{
    // singleton-like holder for easy access in render methods
    public static MetricsSnapshot Current => _current;
    private static MetricsSnapshot _current = new();


    public double Tps { get; set; }
    public double P50 { get; set; }
    public double P95 { get; set; }
    public double P99 { get; set; }
    public double Min { get; set; }
    public double Max { get; set; }
    public double ErrorsPct { get; set; }
    public long MessagesTotal { get; set; }
    public TimeSpan TimeRunning { get; set; }


    public int Nodes { get; set; } = 1;
    public int Shards { get; set; } = 0;
    public int Entities { get; set; } = 0;
    public Dictionary<string, int> ShardDist { get; set; } = new();
    public string RebalanceStatus { get; set; } = "Idle";


    public ConcurrentQueue<ClusterEvent> RebalanceTimeline { get; } = new();


    public static void Update(MetricsSnapshot next) => _current = next;
}

public enum RunMode { Idle, Warmup, Measure }

public class State
{
    public RunMode Mode { get; set; } = RunMode.Idle;
    public int Parallelism { get; set; } = 4;
    public int Rps { get; set; } = 1000;
}
