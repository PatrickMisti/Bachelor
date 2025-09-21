namespace Infrastructure.PubSub;

public interface IPubMessage;

public interface IPubSubTopic;
public interface IPubSubTopicBackend : IPubSubTopic;
public interface IPubSubTopicAll : IPubSubTopic;
public interface IPubSubTopicIngress : IPubSubTopic;
public interface IPubSubTopicApi : IPubSubTopic;
public interface IPubSubTopicController : IPubSubTopic;

public enum PubSubMember
{
    All,
    Backend,
    Ingress,
    Api,
    Controller
}

public static class PubSubExtension
{
    public static string ToStr(this PubSubMember m) => m switch
    {
        PubSubMember.All => "all",
        PubSubMember.Backend => "backend",
        PubSubMember.Ingress => "ingress",
        PubSubMember.Api => "api",
        PubSubMember.Controller => "controller",
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
        var x when x == typeof(IPubSubTopicController) => PubSubMember.Controller,
        _ => null
    };
}