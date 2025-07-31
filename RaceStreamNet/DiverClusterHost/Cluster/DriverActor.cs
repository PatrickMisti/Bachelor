using Akka.Actor;
using Infrastructure.General;

namespace DiverClusterHost.Cluster;

public class DriverActor : ReceiveActor
{
    private readonly ILogger<DriverActor> _logger;
    private DriverData? _driverData;

    public DriverActor(ILogger<DriverActor> logger)
    {
        _logger = logger;
        _logger.LogInformation("DriverActor constructor");
        // Hier können Nachrichten empfangen und verarbeitet werden
        Receive<string>(message => HandleMessage(message));
        Receive<DriverData>(m =>
        {
            _logger.LogWarning("hallo not functsdafasdflökjlkasjdflkjasdklfjökasjöldfjlökasdjl");
            if (_driverData is null)
            {
                _logger.LogInformation("Create new DriverActor");
                _driverData = m;
            }
            else if (_driverData is not null)
            {
                _logger.LogInformation("update worker on ip: {p}", Context.Self.Path.Address);
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