namespace Infrastructure.General.PubSub;

public static class PubSubExtension
{
    public static string ToStr(this PubSubMember m) => m switch
    {
        PubSubMember.All => "all",
        PubSubMember.Backend => "backend",
        PubSubMember.Ingress => "ingress",
        PubSubMember.Api => "api",
        _ => throw new ArgumentOutOfRangeException(nameof(m), m, null)
    };
}

public static class PubSubTypeMapping
{
    public static PubSubMember? ToMember(Type t) => t switch
    {
        var x when x == typeof(IPubSubTopicAll) => PubSubMember.All,
        var x when x == typeof(IPubSubTopicBackend) => PubSubMember.Backend,
        var x when x == typeof(IPubSubTopicIngress) => PubSubMember.Ingress,
        var x when x == typeof(IPubSubTopicApi) => PubSubMember.Api,
        _ => null
    };
}