using Akka.Actor;
using Akka.Cluster;

namespace SystemConsoleTests;

internal class MemberEventUp
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
}