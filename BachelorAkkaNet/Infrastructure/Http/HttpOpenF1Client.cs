using System.Text.Json;
using Infrastructure.General;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Http;

public class HttpOpenF1Client(HttpClient http, ILogger<HttpOpenF1Client> logger) : IHttpWrapperClient
{
    private readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<IReadOnlyList<PersonalDriverDataDto>?> GetDriversAsync(int sessionKey, CancellationToken? cancellationToken)
    {
        var url = $"/v1/drivers?session_key={sessionKey}";

        var json = await GetStringAsync(url, cancellationToken ?? CancellationToken.None);

        return JsonSerializer.Deserialize<List<PersonalDriverDataDto>>(json, _options);
    }

    public async Task<IReadOnlyList<PositionOnTrackDto>?> GetPositionsOnTrackAsync(int sessionKey, CancellationToken? cancellationToken)
    {
        var url = $"/v1/position?session_key={sessionKey}";

        var json = await GetStringAsync(url, cancellationToken ?? CancellationToken.None);

        return JsonSerializer.Deserialize<List<PositionOnTrackDto>>(json, _options);
    }

    public async Task<IReadOnlyList<IntervalDriverDto>?> GetIntervalDriversAsync(int sessionKey, CancellationToken? cancellationToken)
    {
        var url = $"/v1/intervals?session_key={sessionKey}";

        var json = await GetStringAsync(url, cancellationToken ?? CancellationToken.None);

        return JsonSerializer.Deserialize<List<IntervalDriverDto>>(json, _options);
    }

    public async Task<IReadOnlyList<IOpenF1Dto>?> FetchNextBatch(int sessionKey, CancellationToken? cancellationToken)
    {
        try
        {
            // Parallel laden
            var posTask = GetPositionsOnTrackAsync(sessionKey, cancellationToken);
            var intervalsTask = GetIntervalDriversAsync(sessionKey, cancellationToken);

            await Task.WhenAll(posTask, intervalsTask).ConfigureAwait(false);

            var result = new List<IOpenF1Dto>(
                (posTask.Result?.Count ?? 0) +
                (intervalsTask.Result?.Count ?? 0));

            if (posTask.Result is { Count: > 0 }) result.AddRange(posTask.Result);       // PositionOnTrackDto   : IOpenF1Dto
            if (intervalsTask.Result is { Count: > 0 }) result.AddRange(intervalsTask.Result); // IntervalDriverDto    : IOpenF1Dto

            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "OpenF1PollingClient.FetchNextBatch failed for sessionKey={Session}", sessionKey);
            return null;
        }
    }

    public async Task<IReadOnlyList<RaceSessionDto>?> GetRaceSessionsAsync(int season, SessionTypes types, CancellationToken? cancellationToken = null)
    {
        var url = $"/v1/sessions?year={season}&&session_type={types.ToStr()}";

        var json = await GetStringAsync(url, cancellationToken ?? CancellationToken.None);

        return JsonSerializer.Deserialize<List<RaceSessionDto>>(json, _options);
    }

    private async Task<string> GetStringAsync(string url, CancellationToken token)
    {
        using var resp = await http.GetAsync(url, token);
        resp.EnsureSuccessStatusCode();
        logger.LogInformation("HTTP GET: {base}{uri}", http.BaseAddress!.AbsoluteUri, url);
        return await resp.Content.ReadAsStringAsync(token);
    }
}