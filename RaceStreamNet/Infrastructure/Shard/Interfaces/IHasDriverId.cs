using Infrastructure.Shard.Models;

namespace Infrastructure.Shard.Interfaces;

public interface IHasDriverId
{
    DriverKey Key { get; }
}