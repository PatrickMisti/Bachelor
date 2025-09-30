using Infrastructure.General;

namespace Infrastructure.Http;

public interface IHttpWrapperClient
{
    Task<IReadOnlyList<PersonalDriverDataDto>?> GetDriversAsync(int season, CancellationToken? cancellationToken = null);

    Task<IReadOnlyList<PositionOnTrackDto>?> GetPositionsOnTrackAsync(int sessionKey, CancellationToken? cancellationToken = null);

    Task<IReadOnlyList<IntervalDriverDto>?> GetIntervalDriversAsync(int sessionKey, CancellationToken? cancellationToken = null);

    Task<IReadOnlyList<IOpenF1Dto>?> FetchNextBatch(int sessionKey, CancellationToken? cancellationToken = null);

    Task<IReadOnlyList<RaceSessionDto>?> GetRaceSessionsAsync(int season, SessionTypes types, CancellationToken? cancellationToken = null);
}