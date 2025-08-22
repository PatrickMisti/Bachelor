using System.Net;
using System.Net.Sockets;

namespace Infrastructure.General;

public static class PortChecker
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