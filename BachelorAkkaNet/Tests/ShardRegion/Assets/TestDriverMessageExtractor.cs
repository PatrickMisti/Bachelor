using Akka.Cluster.Sharding;
using Infrastructure.ShardRegion;

namespace Tests.ShardRegion.Assets;

public sealed class TestDriverMessageExtractor(int shards) : HashCodeMessageExtractor(shards)
{
    public override string? EntityId(object message) => message switch
    {
        IHasDriverId m => m.Key.ToString(),
        _ => null
    };

    public override object EntityMessage(object message) => message;
}