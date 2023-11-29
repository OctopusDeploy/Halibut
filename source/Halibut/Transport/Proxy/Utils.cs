using System;
using System.Globalization;
using System.Net.Sockets;

namespace Halibut.Transport.Proxy
{
    internal static class Utils
    {
        internal static string GetHost(TcpClient client)
        {
            if (client == null)
                throw new ArgumentNullException(nameof(client));

            var host = "";
            try
            {
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
                host = ((System.Net.IPEndPoint)client.Client.RemoteEndPoint)!.Address.ToString();
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
            }
            catch
            {   };

            return host;
        }

        internal static string GetPort(TcpClient client)
        {
            if (client == null)
                throw new ArgumentNullException(nameof(client));

            var port = "";
            try
            {
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
                port = ((System.Net.IPEndPoint)client.Client.RemoteEndPoint)!.Port.ToString(CultureInfo.InvariantCulture);
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
            }
            catch
            { };

            return port;
        }

    }
}
