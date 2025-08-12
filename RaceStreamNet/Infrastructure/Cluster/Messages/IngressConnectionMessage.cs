using Akka.Actor;

namespace Infrastructure.Cluster.Messages;

public class IngressConnectionMessage
{
}

public record NotifyIngressShardConnectionClose();


public record IngressConnectToClusterMessage();

public record IngressConnectionRecorded(string Path);

public record IngressConnectionCheckMessage();


// ClusterCoordinator internal resolve

public record IngressResolved(IActorRef Ref);

public record IngressResolveFailed(string Path, Exception Exception);

//