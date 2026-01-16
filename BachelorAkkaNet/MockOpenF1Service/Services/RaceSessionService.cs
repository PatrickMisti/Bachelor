using Infrastructure.Http;
using Infrastructure.Socket.Messages;
using Microsoft.AspNetCore.SignalR;
using MockOpenF1Service.Utilities;

namespace MockOpenF1Service.Services
{
    public class RaceSessionService(ILogger<RaceSessionService> logger, IHttpClientFactory factory) 
        : BaseService(factory.CreateClient(ConfigWrapper.HttpClientOpenF1Name), logger)
    {
        private const string FileName = "races.csv";
        public async Task GetAllRaceSessionsForYear(int year, Hub hub)
        {
            if (!File.Exists(Path.Combine(ConfigWrapper.TempFolderPath, FileName)))
            {
                var url = HttpUtilities.GetRaceSessionForYearUrl(year);
                logger.LogInformation("Loading race session {year} from URL: {url}", year, url);
                var isLoaded = await LoadData(url, FileName);
                if (isLoaded is null)
                {
                    logger.LogWarning("Warning driverNotFound");
                    await hub.Clients.Caller.SendAsync(ConfigWrapper.DriverInfoHubName, GetDriverInfoMessage.Error());
                    return;
                }
                HttpUtilities.ExistOrCreateDir(ConfigWrapper.TempFolderPath);
                await File.WriteAllBytesAsync(Path.Combine(ConfigWrapper.TempFolderPath, FileName), isLoaded);
            }


            await RunFromFile(
                Path.Combine(ConfigWrapper.TempFolderPath, FileName),
                async line =>
                {
                    var split = line.Split(',');
                    var msg = new RaceSessionDto(
                        MeetingKey: int.Parse(split[9]),
                        SessionKey: int.Parse(split[10]),
                        Location: split[8],
                        DateStart: DateTime.Parse(split[6]),
                        DateEnd: DateTime.Parse(split[5]),
                        SessionType: split[12],
                        SessionName: split[11],
                        CountryKey: int.Parse(split[3]),
                        CountryName: split[4],
                        CountryCode: split[2],
                        CircuitKey: int.Parse(split[0]),
                        CircuitShortName: split[1],
                        GmtOffset: split[7],
                        Year: int.Parse(split[13])
                    );

                    await hub.Clients.Caller.SendAsync(
                        ConfigWrapper.RaceSessionHubNameResponse,
                        GetRaceSessionMessage.CreateStart(msg));
                });

            await hub.Clients.Caller.SendAsync(
                ConfigWrapper.RaceSessionHubNameResponse,
                GetRaceSessionMessage.CreateStart(null, SignalStatus.End));
        }
    }
}
