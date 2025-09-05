using Akka.Cluster.Sharding;
using Infrastructure.Shard.Interfaces;

namespace Infrastructure.Testing;

public class DriverMessageExtractorTest() : HashCodeMessageExtractor(10)
{
    public override string? EntityId(object message)=> message switch
    {
        IHasDriverId data when !string.IsNullOrWhiteSpace(data.Key.ToString()) => data.Key.ToString(),
        _ => throw new ArgumentException($"Unexpected message type for EntityId: {message.GetType().Name}")
    };

    public override object EntityMessage(object message) => message;
}