using Infrastructure.Models;

namespace Infrastructure.Cluster.Messages.ResponseMessage;

public sealed class GetDriverStateResponse
{
    public string DriverId { get; }
    public DriverState DriverState { get; }
    public bool IsSuccess { get; }
    public string? ErrorMessage { get; }
    public GetDriverStateResponse(string driverId, DriverState driverState)
    {
        DriverId = driverId;
        DriverState = driverState;
        IsSuccess = true;
        ErrorMessage = null;
    }

    public GetDriverStateResponse(string driverId, string errorMessage)
    {
        DriverId = driverId;
        DriverState = new DriverState();
        IsSuccess = false;
        ErrorMessage = errorMessage;
    }
}