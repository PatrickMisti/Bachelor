using Infrastructure.Models;
using Infrastructure.Shard.Interfaces;

namespace Infrastructure.Shard.Messages;

public sealed class UpdatedDriverMessage : IHasDriverId
{
    public string DriverId { get; } = string.Empty;

    public DriverState State { get; set; }

    public UpdatedDriverMessage()
    {
        DriverId = string.Empty;
        State = new DriverState();
    }

    public UpdatedDriverMessage(string driverId, DriverState state)
    {
        DriverId = driverId;
        State = state;
    }
}