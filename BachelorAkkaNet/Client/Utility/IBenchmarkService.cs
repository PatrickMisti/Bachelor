using Infrastructure.General;

namespace Client.Utility;

public interface IBenchmarkService
{
    event Action<MetricsUpdate>? Metrics;
    event Action<MetricsSnapshot>? ClusterNodes;
    event Action<DriverInfoState>? DriverInfoUpdate;

    Task StartAsync();
    Task<IEnumerable<RaceSession>?> CheckMeasuringAsync();
    Task CheckConnectionsAsync();
    void Stop();
    Task StartSelectedRace(RaceSession race);
    Task StartSelectedRaceByRegion(RaceSession race);
    Task ChangePipelineMode();

    void KillPoll();
}