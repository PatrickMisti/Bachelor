using Akka.Cluster.Sharding;
using Infrastructure.Shard.Messages;
using System.Text.RegularExpressions;

namespace Infrastructure.Shard;

public class DriverMessageExtractor(int maxNumberOfShards = 100) : HashCodeMessageExtractor(maxNumberOfShards)
{
    private static readonly Regex IdRegex = new(@"^[A-Z]{3}(_\d{1,2})?$", RegexOptions.Compiled);

    public override string EntityId(object message)
    {
        string? id = message switch
        {
            UpdateDriverTelemetry m => m.DriverId,
            GetDriverState m => m.DriverId,
            _ => null
        };

        // check if regex is correct else no routing
        if (string.IsNullOrWhiteSpace(id) || !IdRegex.IsMatch(id))
            return null!;

        return id!;
    }

    public override object EntityMessage(object message) => message;
}