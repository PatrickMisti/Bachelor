using Infrastructure.General.Message;
using Infrastructure.Models;

namespace Infrastructure.Cluster.Messages.Notification;

public record NotifyDriverStateMessage(string DriverId, DriverState State) : IPubMessage
{
    public override string ToString()
    {
        return $"NotifyDriverStateMessage: DriverId={DriverId}, State={State}";
    }
}