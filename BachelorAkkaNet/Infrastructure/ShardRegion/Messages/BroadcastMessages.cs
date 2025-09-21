using Infrastructure.PubSub;
using Newtonsoft.Json;

namespace Infrastructure.ShardRegion.Messages;

public sealed class GetDriverStateRequest : IHasDriverId, IPubMessage
{
    public DriverKey? Key { get; set; }

    [JsonConstructor]
    public GetDriverStateRequest()
    { }


    public GetDriverStateRequest(int driverNumber, int sessionKey)
    {
        Key = DriverKey.Create(sessionKey, driverNumber);
    }

    public GetDriverStateRequest(DriverKey key)
    {
        Key = key;
    }

    public override string ToString()
    {
        return $"GetDriverStateRequest: {Key}";
    }
}

public sealed class GetDriverStateResponse : IPubMessage
{
    public DriverKey? Key { get; }
    public DriverStateDto? DriverState { get; }
    public bool IsSuccess { get; }
    public string ErrorMessage { get; } = string.Empty;

    [JsonConstructor]
    public GetDriverStateResponse(DriverKey? key, DriverStateDto? driverState, bool isSuccess, string error)
    {
        Key = key;
        DriverState = driverState;
        IsSuccess = isSuccess;
        ErrorMessage = error;
    }

    public GetDriverStateResponse(DriverKey? key, DriverStateDto? driverState)
    {
        Key = key;
        DriverState = driverState;
        IsSuccess = key is not null && driverState is not null;
    }


    public GetDriverStateResponse(DriverKey? driverId, string errorMessage)
    {
        Key = driverId ?? null;
        DriverState = null;
        IsSuccess = false;
        ErrorMessage = errorMessage;
    }

    public override string ToString()
    {
        return "GetDriverStateResponse { DriverId: " + Key + ", IsSuccess: " + IsSuccess + ", ErrorMessage: " + ErrorMessage + ", DriverState: " + (DriverState != null ? DriverState.ToString() : "null") + " }";
    }
}