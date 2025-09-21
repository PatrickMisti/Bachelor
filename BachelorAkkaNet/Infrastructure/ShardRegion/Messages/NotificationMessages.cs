using Infrastructure.PubSub;

namespace Infrastructure.ShardRegion.Messages;

public record NotifyDriverStateMessage(DriverKey? Key, DriverStateDto? State) : IPubMessage
{
    public NotifyDriverStateMessage(int driverId, int sessionKey, DriverStateDto state)
        : this(DriverKey.Create(sessionKey, driverId), state)
    {
    }

    public override string ToString()
    {
        return $"NotifyDriverStateMessage: DriverId={Key}";
    }
}