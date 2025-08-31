using System.Collections.Immutable;

namespace Infrastructure.General;

public enum ClusterMemberEnum
{
    Controller,
    Backend,
    Api,
    Ingress
}

public static class ClusterMemberExtension {
    public static string ToStr(this ClusterMemberEnum m) => m switch
    {
        ClusterMemberEnum.Controller => "controller",
        ClusterMemberEnum.Backend => "backend",
        ClusterMemberEnum.Api => "api",
        ClusterMemberEnum.Ingress => "ingress",
        _ => throw new ArgumentOutOfRangeException(nameof(m), m, null)
    };

    public static bool TryParse(string role, out ClusterMemberEnum member)
    {
        member = role.ToLower() switch
        {
            "controller" => ClusterMemberEnum.Controller,
            "backend" => ClusterMemberEnum.Backend,
            "api" => ClusterMemberEnum.Api,
            "ingress" => ClusterMemberEnum.Ingress,
            _ => ClusterMemberEnum.Controller
        };
        return Enum.IsDefined(typeof(ClusterMemberEnum), member);
    }

    public static ClusterMemberEnum Parse(string? role)
    {
        if (role is null) 
            throw new ArgumentNullException(nameof(role));

        if (TryParse(role, out var member))
            return member;

        throw new ArgumentOutOfRangeException(nameof(role), role, null);
    }

    public static bool Contains(this ClusterMemberEnum m, string role) => m.ToStr() == role.ToLower();
    public static bool Contains(this ClusterMemberEnum m, ImmutableHashSet<string> role) => role.Contains(m.ToStr());
}