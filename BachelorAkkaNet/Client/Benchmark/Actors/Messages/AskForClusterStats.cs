namespace Client.Benchmark.Actors.Messages;

public sealed record AskForClusterStatsRequest(string TypeName);

public sealed record AskForClusterStatsResponse(
    int Shards,
    int Entities,
    Dictionary<string, int> ShardDistribution,
    string Pipeline
);
