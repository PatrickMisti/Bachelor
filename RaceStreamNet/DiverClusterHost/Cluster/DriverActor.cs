using Akka.Actor;
using Infrastructure.General;

namespace DiverClusterHost.Cluster;

public class DriverActor : ReceiveActor
{
    private readonly ILogger<DriverActor> _logger;
    private DriverData _driverData = null!;

    public DriverActor(ILogger<DriverActor> logger)
    {
        _logger = logger;
        // Hier können Nachrichten empfangen und verarbeitet werden
        Receive<string>(message => HandleMessage(message));
        Receive<DriverData>(m =>
        {
            _logger.LogInformation("hallo not funct");
            Sender.Tell($"Received driver with ID {m.DriverId}");
        });

    }
    private void HandleMessage(string message)
    {
        // Logik zur Verarbeitung der Nachricht
        Console.WriteLine($"Nachricht empfangen: {message}");
    }
}