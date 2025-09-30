using Akka.Actor;
using Akka.Cluster.Tools.Singleton;
using Infrastructure.General;

namespace Client.Utility;

public static class Percentiles
{
    public static double P(double[] sorted, double q)
    {
        if (sorted.Length == 0) return 0;
        var idx = (int)Math.Clamp(q * (sorted.Length - 1), 0, sorted.Length - 1);
        return sorted[idx];
    }
}

public class RollingWindow<T> where T : struct, IComparable<T>
{
    private readonly int _cap;
    private readonly Queue<T> _q = new();
    public RollingWindow(int capacity) => _cap = Math.Max(1, capacity);
    public void Add(T v)
    {
        _q.Enqueue(v);
        while (_q.Count > _cap) _q.Dequeue();
    }
    public T[] ToArraySorted()
    {
        var arr = _q.ToArray();
        Array.Sort(arr);
        return arr;
    }
}

public static class ProxyGenerator
{
    public static ClusterSingletonProxySettings CreateProxySettings(ActorSystem system, ClusterMemberRoles role) =>
        ClusterSingletonProxySettings
            .Create(system)
            .WithRole(role.ToStr())
            .WithSingletonName(role.ToStr());

    public static IActorRef CreateProxy(this ActorSystem system, ClusterMemberRoles role)
    {
        var proxySettings = CreateProxySettings(system, role);

        return system.ActorOf(
            props: ClusterSingletonProxy.Props(
                singletonManagerPath: $"/user/{role.ToStr()}",
                settings: proxySettings),
            name: $"{role.ToStr()}-api-proxy");
    }
}