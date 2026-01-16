using Infrastructure.Http;
using Infrastructure.Socket.Messages;
using Microsoft.AspNetCore.SignalR;
using MockOpenF1Service.Utilities;
using System.Net.Http;
using System.Text;

namespace MockOpenF1Service.Services;

public class BaseService(HttpClient httpClient, ILogger logger)
{
    protected async Task<bool> LoadDataFromUrl(int sessionKey, string url, string fileName)
    {
        try
        {
            var csvBytes = await LoadData(url, fileName);

            if (csvBytes is null) 
                return false;

            return await SaveDriverInfoBySession(csvBytes, sessionKey, fileName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load driver csv (sessionKey={SessionKey})", sessionKey);
            return false;
        }
    }

    protected async Task<byte[]?> LoadData(string url, string fileName)
    {
        try
        {
            var response = await httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Warning driverNotFound");
                return null;
            }
            return await response.Content.ReadAsByteArrayAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load driver csv (url={i})", url);
            return null;
        }
    }

    private async Task<bool> SaveDriverInfoBySession(byte[] csv, int sessionKey, string fileName)
    {
        try
        {
            HttpUtilities.ExistOrCreateDir(ConfigWrapper.TempFolderPath);
            HttpUtilities.ExistOrCreateDir(Path.Combine(ConfigWrapper.TempFolderPath, sessionKey.ToString()));

            await File.WriteAllBytesAsync(ConfigWrapper.GetFileNameFromDriverInfo(sessionKey, fileName), csv);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save driver csv (sessionKey={SessionKey})", sessionKey);
            return false;
        }
    }

    protected async Task RunFromFile(string path, Func<string, Task> action)
    {
        await using var fs = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            useAsync: true
        );

        using var sr = new StreamReader(fs, Encoding.UTF8);
        var headerLine = await sr.ReadLineAsync();
        logger.LogInformation("Header {0}", headerLine);

        while (await sr.ReadLineAsync() is { } line)
        {
            action?.Invoke(line);
        }
    }
}