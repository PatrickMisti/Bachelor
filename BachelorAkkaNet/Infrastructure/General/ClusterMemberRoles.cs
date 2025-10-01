using System.Collections.Immutable;

namespace Infrastructure.General;

public enum ClusterMemberRoles
{
    Controller,
    Backend,
    Api,
    Ingress
}

public enum SessionTypes
{
    Practice,
    Qualifying,
    Race
}

public enum Mode { None, Push, Polling }
public static class ClusterMemberExtension
{
    public static string ToStr(this ClusterMemberRoles m) => m switch
    {
        ClusterMemberRoles.Controller => "controller",
        ClusterMemberRoles.Backend => "backend",
        ClusterMemberRoles.Api => "api",
        ClusterMemberRoles.Ingress => "ingress",
        _ => throw new ArgumentOutOfRangeException(nameof(m), m, null)
    };

    public static bool TryParse(string role, out ClusterMemberRoles member)
    {
        member = role.ToLower() switch
        {
            "controller" => ClusterMemberRoles.Controller,
            "backend" => ClusterMemberRoles.Backend,
            "api" => ClusterMemberRoles.Api,
            "ingress" => ClusterMemberRoles.Ingress,
            _ => ClusterMemberRoles.Controller
        };
        return Enum.IsDefined(typeof(ClusterMemberRoles), member);
    }

    public static ClusterMemberRoles Parse(string? role)
    {
        if (role is null)
            throw new ArgumentNullException(nameof(role));

        if (TryParse(role, out var member))
            return member;

        throw new ArgumentOutOfRangeException(nameof(role), role, null);
    }

    public static string ToStr(this SessionTypes t) => t switch
    {
        SessionTypes.Practice => "Practice",
        SessionTypes.Qualifying => "Qualifying",
        SessionTypes.Race => "Race",
        _ => throw new ArgumentOutOfRangeException(nameof(t), t, null)
    };

    public static bool Contains(this ClusterMemberRoles m, string role) => m.ToStr() == role.ToLower();
    public static bool Contains(this ClusterMemberRoles m, ImmutableHashSet<string> role) => role.Contains(m.ToStr());
}