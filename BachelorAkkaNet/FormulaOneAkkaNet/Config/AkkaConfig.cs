using System.Net;
using System.Net.Sockets;

namespace FormulaOneAkkaNet.Config;

public class AkkaConfig
{
    // Default hostname is localhost
    public string Hostname { get; set; } = "localhost";

    // Default port is 0, which means a random port will be assigned
    public required int Port { get; set; }

    // Default cluster name
    public string ClusterName { get; set; } = "cluster-system";

    // List of seed nodes for the cluster
    public string[] SeedNodes { get; set; } = [
        "akka.tcp://cluster-system@localhost:5000", 
        "akka.tcp://cluster-system@localhost:6000"
    ];

    // Roles for the cluster nodes
    // Roles what this node can perform
    // Should be split else all nodes will have all roles
    public List<string> Roles { get; set; } = new();

    // Default role for the cluster nodes
    public required string Role { get; set; }

    // Shard name for the cluster
    public string ShardName { get; set; } = "driver";
}

internal static class PortChecker
{
    public static int CheckPort(int port)
    {
        for (int i = 0; i < 20; i++)
        {
            bool isPortAvailable = true;
            int portToCheck = port + i;

            try
            {
                using var listener = new TcpListener(IPAddress.Loopback, portToCheck);
                listener.Start();
                listener.Stop();
            }
            catch (SocketException)
            {
                isPortAvailable = false;
            }

            if (isPortAvailable)
                return port + i;
        }

        throw new InvalidOperationException($"Port {port} is not available after 20 attempts.");
    }
}