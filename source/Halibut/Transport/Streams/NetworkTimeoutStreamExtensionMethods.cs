using System.IO;
using System.Net.Sockets;

namespace Halibut.Transport.Streams
{
    public static class NetworkTimeoutStreamExtensionMethods
    {
        internal static NetworkTimeoutStream AsNetworkTimeoutStream(this Stream stream)
        {
            return new NetworkTimeoutStream(stream);
        }

        internal static NetworkTimeoutStream GetNetworkTimeoutStream(this TcpClient client)
        {
            return new NetworkTimeoutStream(client.GetStream());
        }
    }
}
