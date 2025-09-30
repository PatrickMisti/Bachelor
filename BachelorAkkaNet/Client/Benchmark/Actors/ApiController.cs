using System.Diagnostics;
using Akka.Actor;
using Akka.Event;
using Client.Benchmark.Actors.Messages;
using Client.Utility;
using Infrastructure.General;
using Infrastructure.Http;
using Infrastructure.PubSub;
using Infrastructure.PubSub.Messages;

namespace Client.Benchmark.Actors;

public sealed class ApiController : ReceivePubSubActor<IPubSubTopicApi>
{
    private ILoggingAdapter Logger => Context.GetLogger();
    private readonly IActorRef _controller;
    private readonly IActorRef _ingress;
    private readonly IMetricsSink _metricsSink;

    public ApiController(IActorRef proxyController, IActorRef proxyIngress, IMetricsSink sink)
    {
        Logger.Debug("Start Api Controller");
        _controller = proxyController;
        _ingress = proxyIngress;
        _metricsSink = sink;
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

        ReceiveAsync<AskForRaceSessionsRequest>(async _ =>
        {
            Logger.Info("Call ingress for race sessions");
            var sw = Stopwatch.StartNew();
            var success = false;
            int messagesCount = 0;

            try
            {
                var resp = await _ingress.Ask<HttpGetRaceSessionsResponse>(
                    new HttpGetRaceSessionsRequest(Year: 2023, Types: SessionTypes.Race),
                    TimeSpan.FromSeconds(5));

                success = resp.IsSuccess;

                if (!resp.IsSuccess)
                {
                    Logger.Warning("Error from ingress: {0}", resp.ErrorMessage);
                    Sender.Tell(new AskForRaceSessionsResponse(resp.ErrorMessage!));
                }
                else
                {
                    messagesCount = resp.Sessions.Count;
                    Sender.Tell(new AskForRaceSessionsResponse(resp.Sessions.AsEnumerable()));
                }
            }
            catch (AskTimeoutException e)
            {
                Logger.Warning("Ask timeout to ingress via proxy: {0}", e.Message);
            }
            finally
            {
                sw.Stop();
                _metricsSink.Publish(new MetricsSample(sw.Elapsed.TotalMilliseconds, success, messagesCount));
            }
        });
    }

    public static Props Prop(IActorRef proxyController, IActorRef proxyIngress, IMetricsSink sink) => 
        Props.Create(() => new ApiController(proxyController,proxyIngress, sink));
}