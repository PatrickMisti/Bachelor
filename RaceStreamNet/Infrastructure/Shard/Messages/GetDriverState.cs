using Infrastructure.Shard.Interfaces;

namespace Infrastructure.Shard.Messages;

public sealed record GetDriverState(string DriverId) : IHasDriverId;