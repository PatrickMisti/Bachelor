using Akka.Actor;

namespace Infrastructure.Cluster.Messages;

public record RegisterShardRequest;

public record RegisterShardResponse
{
    public bool IsSuccess { get; init; }
    public IActorRef? ShardRef { get; init; }
    public string? ErrorMessage { get; init; }
    public static RegisterShardResponse Success(IActorRef sh) => new() { IsSuccess = true, ShardRef = sh};
    
    public static RegisterShardResponse Failure(string errorMessage) => new() { IsSuccess = false, ErrorMessage = errorMessage };
}

public record RegisteredShard(string Path);

