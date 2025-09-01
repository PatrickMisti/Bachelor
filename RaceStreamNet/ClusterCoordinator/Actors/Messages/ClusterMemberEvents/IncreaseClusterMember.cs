using Akka.Actor;

namespace ClusterCoordinator.Actors.Messages.ClusterMemberEvents;

public record IncreaseClusterMember(Address ClusterMemberRef) : UpdateClusterCount(ClusterMemberRef);