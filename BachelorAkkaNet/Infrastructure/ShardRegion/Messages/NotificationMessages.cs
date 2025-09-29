using Infrastructure.PubSub;

namespace Infrastructure.ShardRegion.Messages;

public record NotifyDriverStateMessage : IPubMessage
{
    public DriverKey Key { get; set; }
    public DriverStateDto State { get; set; }


    public NotifyDriverStateMessage(DriverKey key, DriverStateDto? state)
    {
        Key = key;
        State = state ?? null!;
    }


    public override string ToString()
    {
        return $"NotifyDriverStateMessage: DriverId={Key}";
    }
}