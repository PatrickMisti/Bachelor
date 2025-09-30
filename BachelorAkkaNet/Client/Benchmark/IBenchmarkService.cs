using Client.Utility;

namespace Client.Benchmark;

public interface IBenchmarkService
{
    event Action<MetricsUpdate>? Metrics;
    event Action<MetricsSnapshot>? ClusterNodes;

    Task StartAsync();
    Task CheckMeasuringAsync();
    Task CheckConnectionsAsync();
    void Stop();

    //event EventHandler<ClusterEvent>? ClusterEvent;


/*    Task StartHeartbeatAsync(CancellationToken stop);
    Task StartWarmupAsync(int durationSec);
    Task StartMeasureAsync(int durationSec, int parallelism, int rps);
    Task TriggerBurstAsync(int count);
    Task RampUpAsync(int startRps, int endRps, int seconds);
    Task AddNodeAsync();
    Task KillNodeAsync();
    Task ExportCsvAsync(string filePath);*/
}