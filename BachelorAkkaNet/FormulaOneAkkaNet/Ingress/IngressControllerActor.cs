using Akka.Actor;
using Akka.Event;
using Akka.Hosting;
using Akka.Streams;
using FormulaOneAkkaNet.Ingress.Messages;
using Infrastructure;
using Infrastructure.General;
using Infrastructure.Http;
using Infrastructure.PubSub;
using Infrastructure.PubSub.Messages;
using Infrastructure.SocketMessages;
using Microsoft.AspNetCore.SignalR.Client;

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
                StartPipelineWithMode(worker: _workerCount);
        });

        ReceiveAsync<ShardConnectionAvailableRequest>(async _ =>
        {
            var res = await _controller.Ask<IngressConnectivityResponse>(IngressConnectivityRequest.Instance);
            _log.Info($"Got response from controller with shard is online: {res.ShardAvailable}");
            Sender.Tell(new ShardConnectionAvailableResponse(res.ShardAvailable));

            if (res.ShardAvailable && !_pipeline.IsRunning)
                StartPipelineWithMode(_workerCount);
        });

        ReceiveAsync<IOpenF1Dto>(async dto =>
        {
            var accepted = await _pipeline.OfferAsync(dto);
            if (!accepted)
                _log.Debug("DTO dropped (pipeline not in push mode).");
        });

        ReceiveAsync<IngressSessionRaceMessage>(async msg =>
        {
            if (_pipeline.IsPushMode)
                await StartPushStreamClient(msg.SessionKey);
            else
                StartPipelineWithMode(_workerCount, msg.SessionKey);
        });

        HandleIngressPipeline();

        // only for testing
        // _log.Info("Send shard connection available request!");
        // Self.Tell(ShardConnectionAvailableRequest.Instance);
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
            _log.Debug("Switch to polling stream pipeline ingress");
            // Set mode to Polling, wait for session key, then actually start
            _pipeline.ChangeMode(Mode.Polling);
            if (_pipeline.IsRunning)
                _pipeline.Stop();
        });

        Receive<StopPipeline>(_ =>
        {
            _log.Debug("Got request to stop ingress stream.");
            _pipeline.Stop();
        });

        Receive<PipelineModeRequest>(_ =>
        {
            _log.Debug($"Ask for mode: {_pipeline.GetMode}");
            Sender.Tell(new PipelineModeResponse(_pipeline.GetMode));

            if (!_pipeline.IsRunning)
                Self.Tell(ShardConnectionAvailableRequest.Instance);
        });

        Receive<PipelineModeChangeRequest>(msg =>
        {
            _log.Debug("Change mode of pipeline");
            var before = _pipeline.GetMode;
            _pipeline.Stop();
            _pipeline.ChangeMode(msg.NewPipelineMode);
            _log.Info($"Changed Mode from {before} to {_pipeline.GetMode}!");
            Sender.Tell(new PipelineModeChangeResponse(_pipeline.GetMode));
        });
    }

    private void StartPipelineWithMode(int worker, int? sessionKey = null)
    {
        if (_pipeline.IsPushMode)
        {
            _pipeline.StartPush(worker);
            return;
        }

        var pollClient = _sp.GetRequiredService<IHttpWrapperClient>();
        if (sessionKey is not null)
            _pipeline.StartPolling(pollClient, (int)sessionKey, worker);
        else
            _log.Info("Polling mode requested but no sessionKey available yet. Waiting for session message...");
    }

    // Demo
    private const string ConnectionUrl = "https://localhost:7086/driverInfoHub";
    private const string DriverInfoState = "driverInfoResponse";
    private const string RaceSessionState = "raceSessionHub";

    private const string DriverInfoInvokeName = "LoadDriverForSessionKey";
    private HubConnection hubConnection = new HubConnectionBuilder()
        .WithUrl(ConnectionUrl)
        .WithAutomaticReconnect()
        .Build();


    private async Task StartPushStreamClient(int sessionKey)
    {
        hubConnection.On<GetDriverInfoMessage>(DriverInfoState, async (payload) =>
        {
            if (payload.Status != SignalStatus.Error)
            {
                await _pipeline.OfferAsync(payload.Driver!);
            }
            else
            {
                _pipeline.Stop();
            }
        });

        hubConnection.On<GetRaceSessionMessage>(RaceSessionState, async payload =>
        {
            if (payload.Status != SignalStatus.Error)
            {
                await _pipeline.OfferAsync(payload.Session!);
            }
            else
            {
                _pipeline.Stop();
            }
        });
        // .....

        //await hubConnection.SendAsync(DriverInfoInvokeName);
        // .....


        // Demo
        if (!_pipeline.IsPushMode || !_pipeline.IsRunning)
        {
            _log.Info("Pipeline ist not online or in false mode!");
            return;
        }

        var http = _sp.GetRequiredService<IHttpWrapperClient>();

        // fire and forget
        var driver = await http.GetDriversAsync(sessionKey, CancellationToken.None);

        _log.Info("Start Http Clipping");
        if (driver is null)
        {
            _log.Warning("No drivers received from OpenF1 API for session {0}", sessionKey);
        }
        else
        {
            await _pipeline.OfferAsync(driver.Cast<IOpenF1Dto>().ToList());
            _log.Info("Sent {0} drivers to pipeline for session {1}", driver.Count, sessionKey);
        }

        var list = await http.FetchNextBatch(sessionKey, CancellationToken.None);
        if (list is null || list.Count == 0)
        {
            _log.Info("No initial data batch received for session {0}", sessionKey);
            return;
        }

        await _pipeline.OfferAsync(list.ToList());

        _log.Info("Sent {0} data points to pipeline for session {1}", list.Count, sessionKey);

        // Not working to many requests
        /*var options = new ParallelOptions { MaxDegreeOfParallelism = 1};
        await Parallel.ForEachAsync(driver!, options, async (dto, token) =>
        {
            var l = await http.GetTelemetryDatesAsync(sessionKey, dto.DriverNumber, token);
            if (l is not null && l.Count > 0)
                await _pipeline.OfferAsync(l.Cast<IOpenF1Dto>().ToList());
        });*/
#if TESTING
        foreach (var dto in driver!)
        {
            var l = await http.GetTelemetryDatesAsync(sessionKey, dto.DriverNumber);
            if (l is not null && l.Count > 0)
                await _pipeline.OfferAsync(l.Cast<IOpenF1Dto>().ToList());
        }
#endif
    }

    protected override void PostStop()
    {
        _log.Debug("Stop Controller!");
        _pipeline.Stop();
        base.PostStop();
    }
}