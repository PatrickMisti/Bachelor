namespace Infrastructure.Shard.Exceptions;

public class DriverInShardNotFoundException : Exception
{
    public string Id { get; private set; }
    public DriverInShardNotFoundException(string driverId)
        : base($"Driver with ID '{driverId}' not found in the shard.")
    {
        Id = driverId;
    }

    public DriverInShardNotFoundException(string driverId, Exception innerException)
        : base($"Driver with ID '{driverId}' not found in the shard.", innerException)
    {
        Id = driverId;
    }
}