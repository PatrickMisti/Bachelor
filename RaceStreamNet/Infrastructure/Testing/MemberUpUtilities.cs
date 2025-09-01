using Akka.Actor;
using Akka.Cluster;

namespace Infrastructure.Testing;

public static class MemberUpUtilities
{
    public static async Task WaitForMemberUp(ActorSystem sys, TimeSpan timeout, Akka.Cluster.Cluster c)
    {
        var deadline = DateTime.UtcNow + timeout;
        var cluster = c;
        while (DateTime.UtcNow < deadline)
        {
            if (cluster.SelfMember.Status == MemberStatus.Up) return;
            await Task.Delay(100);
        }
        throw new TimeoutException("Cluster member did not reach Up in time.");
    }

    public static async Task WaitForMemberUp(ActorSystem sys, string role, TimeSpan timeout, Akka.Cluster.Cluster c)
    {
        var deadline = DateTime.UtcNow + timeout;
        var cluster = c;
        while (DateTime.UtcNow < deadline)
        {
            var self = cluster.SelfMember;
            if (self.Status == MemberStatus.Up && self.HasRole(role))
                return;
            await Task.Delay(200);
        }
        throw new TimeoutException($"Member with role '{role}' did not reach Up within {timeout.TotalSeconds}s.");
    }

    public static async Task WaitForMemberUp(string role, TimeSpan timeout, Akka.Cluster.Cluster cluster)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var members = cluster.State.Members;
            if (members.Any(m => m.Status == MemberStatus.Up && m.HasRole(role)))
                return;
            await Task.Delay(100);
        }
        throw new TimeoutException($"Member with role '{role}' did not reach Up in time.");
    }
}