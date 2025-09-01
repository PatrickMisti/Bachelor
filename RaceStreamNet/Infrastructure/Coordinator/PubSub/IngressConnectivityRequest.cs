using Infrastructure.General.Message;

namespace Infrastructure.Coordinator.PubSub;

public record IngressConnectivityRequest
{
    public static IngressConnectivityRequest Instance => new();

    private IngressConnectivityRequest(){}
}