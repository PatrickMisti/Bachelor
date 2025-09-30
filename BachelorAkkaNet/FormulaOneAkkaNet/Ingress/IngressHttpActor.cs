using Akka.Actor;
using Akka.Event;
using Akka.Hosting;
using Akka.Streams;
using Akka.Streams.Dsl;
using Infrastructure.General;
using Infrastructure.Http;
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
                .. position?.Select(x => x.ToMap())  ?? Enumerable.Empty<IHasDriverId>()
            ];

            if (data.Count == 0)
            {
                _log.Info($"No data found for sessionKey: {sessionKey}");
                Sender.Tell(new HttpStartRaceSessionResponse($"No data for {sessionKey} found!"));
                return;
            }

            _log.Info($"Found {data.Count} data for sessionKey: {sessionKey}");

            var src = Source.From(data).Grouped(200);

            var srcRef = await src.RunWith(StreamRefs.SourceRef<IEnumerable<IHasDriverId>>(),Context.Materializer());

            Sender.Tell(new HttpStartRaceSessionResponse(srcRef));
        }
        catch (Exception ex)
        {
            _log.Warning($"Error throw in http {ex.Message}");
            Sender.Tell(new HttpGetRaceSessionsResponse($"Error: {ex.Message}"));
        }
    }
}