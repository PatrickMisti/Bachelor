using Infrastructure.Models;
using Infrastructure.Shard.Exceptions;

namespace Infrastructure.Shard.Responses;

public sealed record DriverStateResponse
{
    public string DriverId { get; private set; }
    public DriverState? State { get; private set; }
    public Exception? Error { get; private set; }
    public bool IsSuccess { get; private set; }

    public DriverStateResponse() {}

    public DriverStateResponse(string driverId, DriverState state)
    {
        DriverId = driverId;
        State = state;
        IsSuccess = true;
    }

    public DriverStateResponse(string driverId, Exception error)
    {
        DriverId = driverId;
        Error = error;
        IsSuccess = false;
    }

    public DriverStateResponse(DriverInShardNotFoundException e) : this(e.Id, e)
    {
    }

    public static DriverStateResponse Success(string driverId, DriverState state) => new (driverId, state);

    public static DriverStateResponse Failure(DriverInShardNotFoundException e) => new (e);
}