namespace MockOpenF1Service.Utilities;

public class HttpUtilities
{

    public static string GetDriverSessionUrl(int sessionKey) => $"/v1/drivers?session_key={sessionKey}&csv=true";

    public static string GetRaceSessionForYearUrl(int year) => $"/v1/sessions?year={year}";

    public static void ExistOrCreateDir(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }
}