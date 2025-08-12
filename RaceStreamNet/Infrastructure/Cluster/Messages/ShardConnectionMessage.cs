namespace Infrastructure.Cluster.Messages;

public record ShardConnectionMessage(bool IsOpen);

public record ShardTerminatedRequest();

public record ShardTerminatedResponse(bool IsClosed);

public record ShardUnavailableMessage;

public record ShardAvailableMessage;