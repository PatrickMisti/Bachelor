using Akka.Actor;
using Akka.Event;
using Akka.Hosting;
using Akka.Streams;
using Akka.Streams.Dsl;
using FormulaOneAkkaNet.Ingress.Messages;
using Infrastructure.General;
using Infrastructure.Http;
using Infrastructure.PubSub.Messages;
using Infrastructure.ShardRegion;

namespace FormulaOneAkkaNet.Ingress;

public class IngressHttpActor : ReceiveActor
{
    private IHttpWrapperClient _http;
    private IActorRef _controller;
    private ILoggingAdapter _log = Context.GetLogger();

    public IngressHttpActor(IServiceProvider sp, IRequiredActor<IngressControllerActor> controller)
    {
        _http = sp.GetRequiredService<IHttpWrapperClient>();
        _controller = controller.ActorRef;

        ReceiveAsync<HttpGetRaceSessionsRequest>(HandleGetRaceSessions);
        ReceiveAsync<HttpStartRaceSessionRequest>(HandleStartRaceSession);
        Receive<HttpStartRaceSessionWithRegionMessage>(HandleStartRaceWithRegion);
        ReceiveAsync<HttpPipelineModeRequest>(HandlePipelineMode);
        ReceiveAsync<HttpPipelineModeChangeRequest>(HandlePipelineChange);
        Receive<HttpKillPipeline>(HandleKillPipeline);
    }

    private async Task HandleGetRaceSessions(HttpGetRaceSessionsRequest msg)
    {
        _log.Debug($"Select Race with year: {msg.Year} and type: {msg.Types.ToStr()}");

        try
        {
            var sessions = await _http.GetRaceSessionsAsync(msg.Year, msg.Types);

            if (sessions is null || sessions.Count == 0)
            {
                _log.Info($"No sessions found for year: {msg.Year} and type: {msg.Types.ToStr()}");
                Sender.Tell(new HttpGetRaceSessionsResponse("No Races found!"));
                return;
            }

            _log.Debug($"Found {sessions.Count} sessions for year: {msg.Year} and type: {msg.Types.ToStr()}");

            Sender.Tell(new HttpGetRaceSessionsResponse(sessions.Select(i => i.ToMap()).ToList()));
        }
        catch (Exception ex)
        {
            _log.Warning($"Error throw in http {ex.Message}");
            Sender.Tell(new HttpGetRaceSessionsResponse($"Error: {ex.Message}"));
        }
    }

    private async Task HandleStartRaceSession(HttpStartRaceSessionRequest msg)
    {
        _log.Debug($"Grab all Data from OpenF1 with Session_Key: {msg.SessionKey}");
        try
        {
            var sessionKey = msg.SessionKey;

            var drivers = await _http.GetDriversAsync(sessionKey);
            var telemetry = await _http.GetIntervalDriversAsync(sessionKey);
            var position = await _http.GetPositionsOnTrackAsync(sessionKey);


            IReadOnlyList<IHasDriverId> data =
            [
                .. drivers?.Select(x => x.ToMap()) ?? Enumerable.Empty<IHasDriverId>(),
                .. telemetry?.Select(x => x.ToMap()) ?? Enumerable.Empty<IHasDriverId>(),
                .. position?.Select(x => x.ToMap()) ?? Enumerable.Empty<IHasDriverId>()
            ];

            if (data.Count == 0)
            {
                _log.Info($"No data found for sessionKey: {sessionKey}");
                Sender.Tell(new HttpStartRaceSessionResponse($"No data for {sessionKey} found!"));
                return;
            }

            _log.Info($"Found {data.Count} data for sessionKey: {sessionKey}");

            var src = Source.From(data).Grouped(200);

            var srcRef = await src.RunWith(StreamRefs.SourceRef<IEnumerable<IHasDriverId>>(), Context.Materializer());

            Sender.Tell(new HttpStartRaceSessionResponse(srcRef));
        }
        catch (Exception ex)
        {
            _log.Warning($"Error throw in http {ex.Message}");
            Sender.Tell(new HttpGetRaceSessionsResponse($"Error: {ex.Message}"));
        }
    }

    private void HandleStartRaceWithRegion(HttpStartRaceSessionWithRegionMessage msg)
    {
        _log.Debug("Start msg with region");

        _controller.Tell(new IngressSessionRaceMessage(msg.SessionKey));
    }

    private async Task HandlePipelineMode(HttpPipelineModeRequest msg)
    {
        _log.Debug("Request mode of pipeline");

        var res = await _controller.Ask<PipelineModeResponse>(PipelineModeRequest.Instance);

        Sender.Tell(new HttpPipelineModeResponse(res.PipelineMode));
    }

    private async Task HandlePipelineChange(HttpPipelineModeChangeRequest msg)
    {
        _log.Debug("Change Mode of Pipeline");

        var mode = await _controller.Ask<PipelineModeResponse>(PipelineModeRequest.Instance);

        var res = await _controller.Ask<PipelineModeChangeResponse>(new PipelineModeChangeRequest(mode.PipelineMode == Mode.Polling ? Mode.Push : Mode.Polling));

        Sender.Tell(new HttpPipelineModeChangeResponse(res.PipelineMode));
    }

    private void HandleKillPipeline(HttpKillPipeline msg)
    {
        _log.Debug("Kill Pipeline");
        _controller.Tell(StopPipeline.Instance);
    }
}