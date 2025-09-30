using Akka.Actor;
using Akka.Event;
using Akka.Hosting;
using Infrastructure.General;
using Infrastructure.Http;

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
}