namespace DriverTelemetryIngress.Bridge;

public interface IHttpWrapperClient
{
    Task<IReadOnlyList<PersonalDriverDataDto>?> GetDriversAsync(int season, CancellationToken? cancellationToken = null);

    Task<IReadOnlyList<PositionOnTrackDto>?> GetPositionsOnTrackAsync(int sessionKey, CancellationToken? cancellationToken = null);

    Task<IReadOnlyList<IntervalDriverDto>?> GetIntervalDriversAsync(int sessionKey, CancellationToken? cancellationToken = null);

    Task<IReadOnlyList<IOpenF1Dto>?> FetchNextBatch(int sessionKey, CancellationToken? cancellationToken = null);
}