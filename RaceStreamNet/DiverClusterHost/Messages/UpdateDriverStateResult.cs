namespace DiverShardHost.Messages;

internal record UpdateDriverStateResult(string DriverId, bool Success, string? ErrorMessage = null)
{
    public static UpdateDriverStateResult Done => new ("", true);

    public static UpdateDriverStateResult Error(string driverId, string errorMessage) 
        => new(driverId, false, errorMessage);
}