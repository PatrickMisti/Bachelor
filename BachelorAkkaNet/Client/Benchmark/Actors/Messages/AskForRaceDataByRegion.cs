namespace Client.Benchmark.Actors.Messages;

public record AskForRaceDataByRegionRequest(int SessionKey);

public class AskForRaceDataByRegionResponse
{
    public AskForRaceDataByRegionResponse(Dictionary<string, int> data)
    {
        Data = data;
        ErrorMessage = null;
    }
    public AskForRaceDataByRegionResponse(string errorMessage)
    {
        Data = new Dictionary<string, int>();
        ErrorMessage = errorMessage;
    }
    public Dictionary<string, int> Data { get; }
    public string? ErrorMessage { get; }
}