using Infrastructure.General.Message;

namespace Infrastructure.Coordinator;

public record NotifyIngressShardIsOnline(bool IsOnline) : IPubMessage;