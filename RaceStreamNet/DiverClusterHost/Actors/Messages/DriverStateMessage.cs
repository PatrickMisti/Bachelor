using Infrastructure.Shard.Exceptions;
using Infrastructure.Shard.Models;

namespace DriverShardHost.Actors.Messages;

public sealed record DriverStateMessage
{
    public DriverKey? DriverId { get; private set; }
    public DriverStateDto? State { get; private set; }
    public Exception? Error { get; private set; }
    public bool IsSuccess { get; private set; }

    public DriverStateMessage() {}

    public DriverStateMessage(DriverKey? driverId, DriverStateDto? state)
    {
        DriverId = driverId;
        State = state;
        IsSuccess = (driverId is not null) && (true);
    }

    public DriverStateMessage(DriverKey driverId, Exception error)
    {
        DriverId = driverId;
        Error = error;
        IsSuccess = false;
    }

    public DriverStateMessage(DriverInShardNotFoundException e) : this(e.Id, e)
    {
    }

    public static DriverStateMessage Success(DriverKey? driverId, DriverStateDto? state) => new (driverId, state);

    public static DriverStateMessage Failure(DriverInShardNotFoundException e) => new (e);
}