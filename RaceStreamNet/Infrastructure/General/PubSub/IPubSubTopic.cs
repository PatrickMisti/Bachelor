namespace Infrastructure.General.PubSub;

public interface IPubSubTopic;
public interface IPubSubTopicBackend : IPubSubTopic;
public interface IPubSubTopicAll : IPubSubTopic;
public interface IPubSubTopicIngress : IPubSubTopic;
public interface IPubSubTopicApi : IPubSubTopic;