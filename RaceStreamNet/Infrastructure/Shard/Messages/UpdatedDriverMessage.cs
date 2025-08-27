using Infrastructure.Models;
using Infrastructure.Shard.Interfaces;

namespace Infrastructure.Shard.Messages;

public sealed class UpdatedDriverMessage(string driverId, DriverState state) : IHasDriverId
{
    public string DriverId { get; } = driverId;

    public DriverState State { get; set; } = state;

    public UpdatedDriverMessage() : this(string.Empty, new())
    {
    }
}