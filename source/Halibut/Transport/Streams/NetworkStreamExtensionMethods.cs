using System.Net.Sockets;

namespace Halibut.Transport.Streams
{
    public static class NetworkStreamExtensionMethods
    {
        internal static NetworkTimeoutStream AsNetworkTimeoutStream(this NetworkStream networkStream)
        {
            return new NetworkTimeoutStream(networkStream);
        }
    }
}
