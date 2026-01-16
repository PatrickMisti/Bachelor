using Akka.Routing;
using Infrastructure.Http;

namespace Infrastructure.Socket.Messages;


public enum SignalStatus
{
    Start,
    InProgress,
    End,
    Error
}

public class GetDriverInfoMessage
{
    public PersonalDriverDataDto? Driver { get; init; }
    public SignalStatus Status { get; init; }

    public static GetDriverInfoMessage CreateStart(PersonalDriverDataDto? driver, SignalStatus? s = null) =>
        new() { Status = s ?? SignalStatus.Start, Driver = driver};

    public static GetDriverInfoMessage Error() =>
        new() { Status = SignalStatus.Error };
}