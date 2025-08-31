using Infrastructure.Models;
using Infrastructure.Shard.Exceptions;

namespace DriverShardHost.Actors.Messages;

public sealed record DriverStateMessage
{
    public string DriverId { get; private set; }
    public DriverState? State { get; private set; }
    public Exception? Error { get; private set; }
    public bool IsSuccess { get; private set; }

    public DriverStateMessage() {}

    public DriverStateMessage(string driverId, DriverState state)
    {
        DriverId = driverId;
        State = state;
        IsSuccess = true;
    }

    public DriverStateMessage(string driverId, Exception error)
    {
        DriverId = driverId;
        Error = error;
        IsSuccess = false;
    }

    public DriverStateMessage(DriverInShardNotFoundException e) : this(e.Id, e)
    {
    }

    public static DriverStateMessage Success(string driverId, DriverState state) => new (driverId, state);

    public static DriverStateMessage Failure(DriverInShardNotFoundException e) => new (e);
}