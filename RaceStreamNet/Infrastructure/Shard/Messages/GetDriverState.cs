using Infrastructure.Shard.Interfaces;

namespace DriverShardHost.Actors.Messages;

public sealed record GetDriverState(string DriverId) : IHasDriverId;