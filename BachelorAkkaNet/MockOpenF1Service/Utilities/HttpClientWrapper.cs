using System.Web;

namespace MockOpenF1Service.Utilities;

public class HttpClientWrapper(HttpClient httpClient)
{
    public const string ApiBaseUrlConfig = "https://api.openf1.org/";

    public const string DriverEndpoint = "v1/drivers";

    public static string GetDriverSessionUrl(int sessionKey) => $"/v1/drivers?session_key={sessionKey}&csv=true";

    public async Task<byte[]> GetDataAsync(string url, params (string key, object value)[] queryParams)
    {
        var builder = new UriBuilder(new Uri(new Uri(ApiBaseUrlConfig), url));
        if (queryParams.Length > 0)
        {
            var query = HttpUtility.ParseQueryString(string.Empty);
            foreach (var (key, value) in queryParams)
            {
                query[key] = value.ToString();
            }
            builder.Query = query.ToString();
        }
        var finalUrl = builder.ToString();
        var response = await httpClient.GetAsync(finalUrl);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync();
    }
}