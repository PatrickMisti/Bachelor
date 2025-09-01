using Akka.Actor;

namespace ClusterCoordinator.Actors.Messages.ClusterMemberEvents;

public record DecreaseClusterMember(Address ClusterMemberRef) : UpdateClusterCount(ClusterMemberRef);