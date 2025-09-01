using Infrastructure.General.Message;

namespace Infrastructure.Coordinator.PubSub;

public record IngressConnectivityRequest : IPubMessage
{
    public static IngressConnectivityRequest Instance => new();

    private IngressConnectivityRequest(){}
}