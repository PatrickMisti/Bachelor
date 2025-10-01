using Akka.Streams;
using Infrastructure.General;
using Infrastructure.ShardRegion;

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

public record HttpStartRaceSessionRequest(int SessionKey);

public record HttpStartRaceSessionResponse
{
    public ISourceRef<IEnumerable<IHasDriverId>> Data { get; init; }
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }

    public HttpStartRaceSessionResponse(ISourceRef<IEnumerable<IHasDriverId>> data)
    {
        Data = data;
        IsSuccess = true;
        ErrorMessage = null;
    }

    public HttpStartRaceSessionResponse(string errorMessage)
    {
        Data = null!;
        IsSuccess = false;
        ErrorMessage = errorMessage;
    }
}

public record HttpStartRaceSessionWithRegionMessage(int SessionKey);

public record HttpPipelineModeRequest()
{
    public static HttpPipelineModeRequest Instance => new();
}

public record HttpPipelineModeResponse(Mode PMode);

public record HttpPipelineModeChangeRequest();

public record HttpPipelineModeChangeResponse(Mode CurrentMode);

public record HttpKillPipeline;