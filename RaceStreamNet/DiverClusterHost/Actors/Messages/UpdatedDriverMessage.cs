using Infrastructure.Shard.Interfaces;
using Infrastructure.Shard.Models;

namespace DriverShardHost.Actors.Messages;

public sealed class UpdatedDriverMessage(DriverKey key, DriverStateDto? state) : IHasDriverId
{
    public DriverKey Key { get; set; } = key;

    public DriverStateDto? State { get; set; } = state;



}