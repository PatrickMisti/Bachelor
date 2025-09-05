using Akka.Cluster.Sharding;
using System.Text.RegularExpressions;
using Infrastructure.Shard.Interfaces;

namespace Infrastructure.Shard;

public class DriverMessageExtractor(int maxNumberOfShards = 100) : HashCodeMessageExtractor(maxNumberOfShards)
{
    private static readonly Regex IdRegex = new(@"^(?:[1-9]\d{0,2})_\d{4,7}$", RegexOptions.Compiled);

    public override string EntityId(object message)
    {
        string? id = message switch
        {
            IHasDriverId m => m.Key.ToString() ?? null,
            _ => null
        };

        // check if regex is correct else no routing
        if (string.IsNullOrWhiteSpace(id) || !IdRegex.IsMatch(id))
            return null!;

        return id!;
    }

    public override object EntityMessage(object message) => message;
}