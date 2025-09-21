using Akka.Actor;

namespace FormulaOneAkkaNet.Coordinator.Messages;

public interface IUpdateClusterCount
{
    Address ClusterMemberRef { get; init; }
}

public record UpdateClusterCount(Address ClusterMemberRef) : IUpdateClusterCount;

public record IncreaseClusterMember(Address ClusterMemberRef) : UpdateClusterCount(ClusterMemberRef);

public record DecreaseClusterMember(Address ClusterMemberRef) : UpdateClusterCount(ClusterMemberRef);