using Microsoft.AspNetCore.SignalR;

namespace MockOpenF1Service.Services
{
    public class DriverForSessionHub(DriverService driverInfo): Hub
    {
        public async Task Send(int year)
        {
            await Clients.All.SendAsync("msg", year);

            await Clients.Caller.SendAsync("msg", year);
        }

        public async Task LoadDriverForSessionKey(int sessionKey) => 
            await driverInfo.GetDriverInfoForSession(sessionKey, this);
        
    }
}
