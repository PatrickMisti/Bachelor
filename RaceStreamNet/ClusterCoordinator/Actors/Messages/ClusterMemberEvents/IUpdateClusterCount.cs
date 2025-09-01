using Akka.Actor;

namespace ClusterCoordinator.Actors.Messages.ClusterMemberEvents;

public interface IUpdateClusterCount
{
    Address ClusterMemberRef { get; init; }
}

public record UpdateClusterCount(Address ClusterMemberRef) : IUpdateClusterCount;