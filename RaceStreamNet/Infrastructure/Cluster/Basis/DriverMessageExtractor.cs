using Akka.Cluster.Sharding;
using Infrastructure.General;

namespace Infrastructure.Cluster.Basis;

public class DriverMessageExtractor : IMessageExtractor
{
    public string EntityId(object message) => message switch
    {
        DriverData data when !string.IsNullOrWhiteSpace(data.DriverId) => data.DriverId,
        _ => throw new ArgumentException($"Unexpected message type for EntityId: {message?.GetType()?.Name}")
    };

    public string ShardId(object message) => message switch
    {
        DriverData data when !string.IsNullOrWhiteSpace(data.DriverId) =>
            (Math.Abs(data.DriverId.GetHashCode()) % 10).ToString(),
        _ => throw new ArgumentException($"Unexpected message type for ShardId: {message?.GetType()?.Name}")
    };


    public object EntityMessage(object message) => message;


    // internal method to handle entityId directly
    public string ShardId(string entityId, object? messageHint = null) => (Math.Abs(entityId.GetHashCode()) % 10).ToString();
    
}