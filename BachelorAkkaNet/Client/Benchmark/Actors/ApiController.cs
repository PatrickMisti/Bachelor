using Akka.Actor;
using Akka.Event;
using Client.Benchmark.Actors.Messages;
using Infrastructure.PubSub;
using Infrastructure.PubSub.Messages;

namespace Client.Benchmark.Actors;

public sealed class ApiController : ReceivePubSubActor<IPubSubTopicApi>
{
    private ILoggingAdapter Logger => Context.GetLogger();
    private IActorRef _controller;

    public ApiController(IActorRef proxy)
    {
        Logger.Debug("Start Api Controller");
        _controller = proxy;
    }

    public override void Activated()
    {
        ReceiveAsync<AskForNodesInClusterRequest>(async _ =>
        {
            Logger.Info("Call coordinator for nodes in system");
            try
            {
                var resp = await _controller.Ask<NodeInClusterResponse>(
                    NodeInClusterRequest.Instance, TimeSpan.FromSeconds(3));
                Sender.Tell(new AskForNodesInClusterResponse(resp.IsInCluster));
            }
            catch (AskTimeoutException ex)
            {
                Logger.Warning("Ask timeout to controller via proxy: {0}", ex.Message);
            }
        });
    }

    public static Props Prop(IActorRef proxy) => Props.Create(() => new ApiController(proxy));
}