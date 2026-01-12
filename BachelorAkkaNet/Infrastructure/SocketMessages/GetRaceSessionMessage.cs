using Infrastructure.Http;

namespace Infrastructure.SocketMessages;

public class GetRaceSessionMessage
{
    public RaceSessionDto? Session { get; set; }

    public SignalStatus Status { get; set; }

    public static GetRaceSessionMessage CreateStart(RaceSessionDto? session, SignalStatus? s = null) =>
        new() { Status = s ?? SignalStatus.Start, Session = session };

    public static GetRaceSessionMessage Error() =>
        new() { Status = SignalStatus.Error };
}