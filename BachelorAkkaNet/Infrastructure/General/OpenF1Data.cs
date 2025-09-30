namespace Infrastructure.General;


public class RaceSession(int sessionKey, string countryName, string circuitName)
{
    public int SessionKey { get; set; } = sessionKey;
    public string CountryName { get; set; } = countryName;
    public string CircuitName { get; set; } = circuitName;
}