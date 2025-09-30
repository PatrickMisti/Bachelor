using Infrastructure.General;

namespace Client.Benchmark.Actors.Messages;

internal class AskForRaceSessionsRequest()
{
    public static AskForRaceSessionsRequest Instance { get; } = new();
}

internal class AskForRaceSessionsResponse
{
    public AskForRaceSessionsResponse(IEnumerable<RaceSession> sessions)
    {
        Sessions = sessions;
        ErrorMessage = null;
    }

    public AskForRaceSessionsResponse(string errorMessage)
    {
        Sessions = Array.Empty<RaceSession>();
        ErrorMessage = errorMessage;
    }

    public IEnumerable<RaceSession> Sessions { get; }
    public string? ErrorMessage { get; }
}