using System.IO;
using System.Net.Sockets;
using System.Net.WebSockets;
using Halibut.Transport.Protocol;

namespace Halibut.Transport.Streams
{
    public class StreamFactory : IStreamFactory
    {
        public Stream CreateStream(TcpClient client)
        {
            var stream = client.GetStream();
            return new NetworkTimeoutStream(stream);
        }

        public WebSocketStream CreateStream(WebSocket webSocket)
        {
            return new WebSocketStream(webSocket);
        }
    }
}