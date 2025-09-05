using Infrastructure.General.Message;
using Infrastructure.Shard.Models;

namespace Infrastructure.Shard.Messages.Notification;

public record NotifyDriverStateMessage(DriverKey? Key, DriverStateDto? State) : IPubMessage
{
    public NotifyDriverStateMessage(int driverId, int sessionKey, DriverStateDto state) 
        : this(DriverKey.Create( sessionKey, driverId), state)
    {
    }

    public override string ToString()
    {
        return $"NotifyDriverStateMessage: DriverId={Key}";
    }
}