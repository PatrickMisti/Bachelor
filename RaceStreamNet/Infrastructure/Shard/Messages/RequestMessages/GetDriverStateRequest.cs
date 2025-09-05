using Infrastructure.General.Message;
using Infrastructure.Shard.Interfaces;
using Infrastructure.Shard.Models;
using Newtonsoft.Json;

namespace Infrastructure.Shard.Messages.RequestMessages;

public sealed class GetDriverStateRequest : IHasDriverId, IPubMessage
{
    public DriverKey? Key { get; set; }

    [JsonConstructor]
    public GetDriverStateRequest()
    { }

    
    public GetDriverStateRequest(int driverNumber, int sessionKey)
    {
        Key = DriverKey.Create(sessionKey, driverNumber);
    }

    public GetDriverStateRequest(DriverKey key)
    {
        Key = key;
    }

    public override string ToString()
    {
        return $"GetDriverStateRequest: {Key}";
    }
}