namespace MockOpenF1Service.Utilities;

public static class ConfigWrapper
{
    public const string DriverInfoHubName = "/driverInfoHub";
    public const string DriverInfoHubNameResponse = "driverInfoResponse";

    public const string RaceSessionHubName = "/raceSessionHub";
    public const string RaceSessionHubNameResponse = "raceSessionResponse";

    public const string HttpClientOpenF1Name = "OpenF1HttpClient";


    public static string TempFolderPath => Path.Combine(Path.GetTempPath(), TempFolderName);
    private const string TempFolderName = "MockOpenF1Service";

    public static string GetFileNameFromDriverInfo(int sessionKey, string fileName) =>
        Path.Combine(TempFolderPath, sessionKey.ToString(), fileName);

}