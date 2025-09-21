namespace FormulaOneAkkaNet.Coordinator.Messages;

public interface IConnectionUpdateMessage;
public record IngressConnectionUpdateMessage(bool IsConnected) : IConnectionUpdateMessage;
public record ShardConnectionUpdateMessage(bool IsShardOnline) : IConnectionUpdateMessage;

public record IngressConnectionCanActivated(bool IsShardOnline);