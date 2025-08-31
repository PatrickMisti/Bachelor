using Infrastructure.General.Message;
using Infrastructure.Models;
using Newtonsoft.Json;

namespace Infrastructure.Shard.Messages.ResponseMessage;

public sealed class GetDriverStateResponse : IPubMessage
{
    public string DriverId { get; }
    public DriverState DriverState { get; }
    public bool IsSuccess { get; }
    public string ErrorMessage { get; } = string.Empty;

    
    // Alternativ statt parameterlosem Ctor:
    // [JsonConstructor]
    // public GetDriverStateResponse(string id, object? state, string? error) { ... }

    [JsonConstructor]
    public GetDriverStateResponse(string driverId, DriverState driverState, bool isSuccess, string error)
    {
        DriverId = driverId;
        DriverState = driverState;
        IsSuccess = isSuccess;
        ErrorMessage = error;
    }
    
    public GetDriverStateResponse(string driverId, DriverState driverState)
    {
        DriverId = driverId;
        DriverState = driverState;
        IsSuccess = true;
    }

    
    public GetDriverStateResponse(string driverId, string errorMessage)
    {
        DriverId = driverId ?? "";
        DriverState = new DriverState();
        IsSuccess = false;
        ErrorMessage = errorMessage;
    }

    public override string ToString()
    {
        return "GetDriverStateResponse { DriverId: " + DriverId + ", IsSuccess: " + IsSuccess + ", ErrorMessage: " + ErrorMessage + ", DriverState: " + (DriverState != null ? DriverState.ToString() : "null") + " }";
    }
}