using Infrastructure.General;

namespace Infrastructure.Http;

public record HttpGetRaceSessionsRequest(int Year, SessionTypes Types = SessionTypes.Race);

public record HttpGetRaceSessionsResponse
{
    public IReadOnlyList<RaceSession> Sessions { get; init; }

    public bool IsSuccess { get; init; }

    public string? ErrorMessage { get; init; }

    public HttpGetRaceSessionsResponse(IReadOnlyList<RaceSession> sessions)
    {
        Sessions = sessions;
        IsSuccess = true;
        ErrorMessage = null;
    }

    public HttpGetRaceSessionsResponse(string errorMessage)
    {
        Sessions = Array.Empty<RaceSession>();
        IsSuccess = false;
        ErrorMessage = errorMessage;
    }
}