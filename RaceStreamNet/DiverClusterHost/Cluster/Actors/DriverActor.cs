using Akka.Actor;
using Akka.Event;
using Infrastructure.General;

namespace DiverShardHost.Cluster.Actors;

public class DriverActor : ReceiveActor
{
    //private readonly ILoggingAdapter _logger = Logging.GetLogger(Context.System, nameof(DriverActor));
    private readonly ILoggingAdapter _logger = Context.GetLogger();
    private DriverData? _driverData;

    public DriverActor()
    {
        _logger.Info("DriverActor constructor");
        // Hier können Nachrichten empfangen und verarbeitet werden
        Receive<string>(message => HandleMessage(message));
        Receive<DriverData>(m =>
        {
            _logger.Warning("hallo not functsdafasdflökjlkasjdflkjasdklfjökasjöldfjlökasdjl");
            if (_driverData is null)
            {
                _logger.Info("Create new DriverActor");
                _driverData = m;
            }
            else if (_driverData is not null)
            {
                _logger.Info("update worker on ip: {p}", Context.Self.Path.Address);
            }
            Sender.Tell($"Received driver with ID {m.DriverId}");
        });

    }
    private void HandleMessage(string message)
    {
        // Logik zur Verarbeitung der Nachricht
        Console.WriteLine($"Nachricht empfangen: {message}");
    }
}