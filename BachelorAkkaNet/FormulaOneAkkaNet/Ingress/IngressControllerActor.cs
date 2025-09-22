using Akka.Actor;
using Akka.Event;
using Akka.Hosting;
using Akka.Streams;
using FormulaOneAkkaNet.Ingress.Messages;
using Infrastructure;
using Infrastructure.Http;
using Infrastructure.PubSub;
using Infrastructure.PubSub.Messages;

namespace FormulaOneAkkaNet.Ingress;

public class IngressControllerActor : ReceivePubSubActor<IPubSubTopicIngress>
{
    private readonly ILoggingAdapter _log = Context.GetLogger();
    private readonly IActorRef _controller;
    private readonly IngressPipeline _pipeline;
    private readonly IServiceProvider _sp;

    // config worker count
    private readonly int _workerCount = 4; // round-robin workers

    public IngressControllerActor(IRequiredActor<ClusterCoordinatorMarker> controller, IServiceProvider sp)
    {
        _controller = controller.ActorRef;

        var system = Context.System;
        var mat = system.Materializer();

        var driverProxy = ActorRegistry.For(system).Get<DriverRegionProxyMarker>();// IRegisteredActor<DriverRegionMarker>.ActorRef;
        _pipeline = new IngressPipeline(Context.System, mat, driverProxy);
        _sp = sp;
    }

    public override void Activated()
    {
        Receive<NotifyIngressShardIsOnline>(msg =>
        {
            string test = msg.IsOnline ? "online" : "offline";
            _log.Info($"Got notification to shard region {test} from controller!");
            if (msg.IsOnline && !_pipeline.IsRunning)
                _pipeline.StartPush(workerCount: 4);
        });

        ReceiveAsync<ShardConnectionAvailableRequest>(async _ =>
        {
            var res = await _controller.Ask<IngressConnectivityResponse>(IngressConnectivityRequest.Instance);
            _log.Info($"Got response from controller with shard is online: {res.ShardAvailable}");
            Sender.Tell(new ShardConnectionAvailableResponse(res.ShardAvailable));

            if (res.ShardAvailable && !_pipeline.IsRunning)
                _pipeline.StartPush(workerCount: 4);
        });

        ReceiveAsync<IOpenF1Dto>(async dto =>
        {
            var accepted = await _pipeline.OfferAsync(dto);
            if (!accepted)
                _log.Debug("DTO dropped (pipeline not in push mode).");
        });

        ReceiveAsync<IngressSessionRaceMessage>(async msg =>
            await StartPushStreamClient(msg.SessionKey));

        HandleIngressPipeline();
    }

    private void HandleIngressPipeline()
    {
        Receive<UsePushStream>(_ =>
        {
            _log.Debug("Start push stream pipeline ingress");
            _pipeline.StartPush(_workerCount);
        });

        Receive<UsePollingStream>(m =>
        {
            _log.Debug("Start polling stream pipeline ingress");
            var pollClient = _sp.GetRequiredService<IHttpWrapperClient>();
            _pipeline.StartPolling(pollClient, m.Interval, _workerCount);
        });

        Receive<StopPipeline>(_ =>
        {
            _log.Debug("Got request to stop ingress stream.");
            _pipeline.Stop();
        });
    }

    private async Task StartPushStreamClient(int sessionKey)
    {
        var http = _sp.GetRequiredService<IHttpWrapperClient>();
        var list = await http.FetchNextBatch(sessionKey, CancellationToken.None);

        if (_pipeline is { IsPushMode: false, IsRunning: false } /*|| list is null*/)
        {
            _log.Info("Pipeline ist not online or in false mode!");
            return;
        }

        // fire and forget
        var driver = await http.GetDriversAsync(sessionKey, CancellationToken.None);

        _log.Info("Start Http Clipping");
        if (driver is null)
        {
            _log.Warning("No drivers received from OpenF1 API for session {0}", sessionKey);
            return;
        }

        await _pipeline.OfferAsync(driver.ToList<IOpenF1Dto>());

        _log.Info("Sent {0} drivers to pipeline for session {1}", driver.Count, sessionKey);

        await _pipeline.OfferAsync(list.ToList());

        _log.Info("Sent {0} data points to pipeline for session {1}", list.Count, sessionKey);
    }

    protected override void PostStop()
    {
        _log.Debug("Stop Controller!");
        _pipeline.Stop();
        base.PostStop();
    }
}