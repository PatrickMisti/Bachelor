using Akka;
using Akka.Actor;
using Akka.Event;
using Akka.Streams;
using Akka.Streams.Dsl;
using Client.Benchmark.Actors.Messages;
using Client.Utility;
using Infrastructure.General;
using Infrastructure.Http;
using Infrastructure.PubSub;
using Infrastructure.PubSub.Messages;
using Infrastructure.ShardRegion;
using System.Diagnostics;
using Akka.Cluster.Sharding;
using Akka.Util;
using Infrastructure.ShardRegion.Messages;
using Infrastructure.ShardRegion.Utilities;
using Serilog;

namespace Client.Benchmark.Actors;

public sealed class ApiController : ReceivePubSubActor<IPubSubTopicApi>
{
    private ILoggingAdapter Logger => Context.GetLogger();
    private readonly IActorRef _controller;
    private readonly IActorRef _ingress;
    private IActorRef _shardRegionProxy = ActorRefs.Nobody;

    private readonly IMetricsPublisher _metricsSink;

    private const string ShardTypeName = "driver";
    

    public ApiController(IActorRef proxyController, IActorRef proxyIngress, IMetricsPublisher sink)
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
            bool success = false;
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
                _metricsSink.Publish(new ReqEnd(sw.Elapsed.TotalMilliseconds, success, messagesCount));
            }
        });

        ReceiveAsync<AskForRaceDataMessage>(async msg =>
        {
            Logger.Info($"Start with session_key: {msg.SessionKey}");
            int messagesCount = 0;
            bool success = false;
            var sw = Stopwatch.StartNew();

            try
            {
                var resp = await _ingress.Ask<HttpStartRaceSessionResponse>(
                    new HttpStartRaceSessionRequest(msg.SessionKey), TimeSpan.FromSeconds(5));

                if (!resp.IsSuccess)
                {
                    Logger.Warning("Error from ingress: {0}", resp.ErrorMessage);
                }
                else
                {
                    _metricsSink.Publish(new StreamStarted());

                    var runTask = resp.Data.Source
                        .Buffer(8, OverflowStrategy.Backpressure)
                        .Select(batch =>
                        {
                            var list = batch as IList<IHasDriverId> ?? batch.ToList();
                            var cnt = list.Count;

                            if (cnt == 0)
                            {
                                _metricsSink.Publish(new StreamBatch(0, sw.ElapsedMilliseconds, false));
                                return NotUsed.Instance;
                            }

                            Interlocked.Add(ref messagesCount, cnt);
                            _metricsSink.Publish(new StreamBatch(cnt, sw.ElapsedMilliseconds, true));
                            return NotUsed.Instance;
                        }).Recover(ex =>
                        {
                            Logger.Error(ex, "Stream failed unexpectedly");
                            _metricsSink.Publish(new StreamBatch(1, sw.ElapsedMilliseconds, false));
                            return NotUsed.Instance;
                        })
                        .RunWith(Sink.Ignore<NotUsed>(), Context.Materializer());

                    try
                    {
                        await runTask;
                        success = true;
                        _metricsSink.Publish(new StreamEnded(true));
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "Stream aborted");
                        success = false;
                        _metricsSink.Publish(new StreamEnded(false));
                    }
                }
            }
            catch (AskTimeoutException e)
            {
                Logger.Warning("Ask timeout to ingress via proxy: {0}", e.Message);
            }
            finally
            {
                sw.Stop();
                _metricsSink.Publish(new ReqEnd(sw.Elapsed.TotalMilliseconds, success, messagesCount));
            }
        });

        ReceiveAsync<AskForClusterStatsRequest>(async _ =>
        {
            var clusterStats =
                await _shardRegionProxy.Ask<ClusterShardingStats>(
                    new GetClusterShardingStats(TimeSpan.FromSeconds(5)));

            var mode = await _ingress.Ask<HttpPipelineModeResponse>(HttpPipelineModeRequest.Instance);

            var shardDist = new Dictionary<string, int>();
            var shards = 0;
            var entities = 0;

            foreach (var (_, regionStats) in clusterStats.Regions)
            {
                foreach (var (shardId, count) in regionStats.Stats)
                {
                    shards++;
                    entities += count;
                    if (shardDist.TryGetValue(shardId, out var c)) 
                        shardDist[shardId] = c + count;
                    else 
                        shardDist[shardId] = count;
                }
            }

            string m = mode.PMode == Mode.Polling ? "Poll" : "Push";
            Sender.Tell(new AskForClusterStatsResponse(shards, entities, shardDist, m));
        });

        Receive<AskForRaceDataByRegionRequest>(msg =>
            Context.ActorOf(RegionNotifyOnceSessionActor.Props(Sender, _ingress, _metricsSink, msg.SessionKey)));

        ReceiveAsync<AskForChangePipelineModeRequest>(async _ =>
        {
            var res = await _ingress.Ask<HttpPipelineModeChangeResponse>(new HttpPipelineModeChangeRequest());
            string s = res.CurrentMode == Mode.Polling ? "Poll" : "Push";
            Sender.Tell(new AskForChangePipelineModeResponse(s));
        });

        Receive<AskForKillingPollingMessage>(_ => _ingress.Tell(new HttpKillPipeline()));
    }

    public static Props Prop(IActorRef proxyController, IActorRef proxyIngress, IMetricsPublisher sink) => 
        Props.Create(() => new ApiController(proxyController,proxyIngress, sink));

    protected override void PreStart()
    {
        _shardRegionProxy = ClusterSharding.Get(Context.System).StartProxy(
            typeName: ShardTypeName,
            role: ClusterMemberRoles.Backend.ToStr(),
            messageExtractor: new DriverMessageExtractor());

        base.PreStart();
    }
}