using Infrastructure.Http;
using Infrastructure.Socket.Messages;
using Microsoft.AspNetCore.SignalR;
using MockOpenF1Service.Utilities;

namespace MockOpenF1Service.Services;

public class DriverService(IHttpClientFactory factoryHttpClient, ILogger<DriverService> logger) 
    : BaseService(factoryHttpClient.CreateClient(ConfigWrapper.HttpClientOpenF1Name), logger)
{
    private const string FileName = "driverInfo.csv";
    public async Task GetDriverInfoForSession(int sessionKey, Hub hub)
    {
        if (!File.Exists(ConfigWrapper.GetFileNameFromDriverInfo(sessionKey, FileName)))
        {
            var url = HttpUtilities.GetDriverSessionUrl(sessionKey);
            logger.LogInformation("Loading driver info for session {sessionKey} from URL: {url}", sessionKey, url);
            var isLoaded = await LoadDataFromUrl(sessionKey, url, FileName);
            if (!isLoaded)
            {
                logger.LogWarning("Warning driverNotFound");
                await hub.Clients.Caller.SendAsync(ConfigWrapper.DriverInfoHubName, GetDriverInfoMessage.Error());
            }
        }

        await RunFromFile(ConfigWrapper.GetFileNameFromDriverInfo(sessionKey, FileName), async line =>
        {
            var data = line.Split(',');
            var driver = new PersonalDriverDataDto(
                SessionKey: sessionKey,
                DriverNumber: int.Parse(data[2]),
                FirstName: data[3],
                LastName: data[6],
                CountryCode: data[1],
                TeamName: data[11],
                Acronym: data[8]
            );
            await hub.Clients.Caller.SendAsync(
                ConfigWrapper.DriverInfoHubNameResponse,
                GetDriverInfoMessage.CreateStart(driver));
        });

        await hub.Clients.Caller.SendAsync(ConfigWrapper.DriverInfoHubNameResponse,
            GetDriverInfoMessage.CreateStart(null, SignalStatus.End));
    }
}