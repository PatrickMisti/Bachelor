using Infrastructure.Shard.Models;

namespace Infrastructure.Shard.Exceptions;

public class DriverInShardNotFoundException : Exception
{
    public DriverKey Id { get; private set; }
    public DriverInShardNotFoundException(DriverKey driverId)
        : base($"Driver with ID '{driverId}' not found in the shard.")
    {
        Id = driverId;
    }

    public DriverInShardNotFoundException(DriverKey driverId, string innerException)
        : base($"Driver with ID '{driverId}' not found in the shard. " +  innerException)
    {
        Id = driverId;
    }
}