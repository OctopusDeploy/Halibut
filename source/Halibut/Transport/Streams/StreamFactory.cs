using System.IO;
using System.Net.Sockets;
using System.Net.WebSockets;
using Halibut.Transport.Protocol;
using Halibut.Util;

namespace Halibut.Transport.Streams
{
    public class StreamFactory : IStreamFactory
    {
        public StreamFactory(AsyncHalibutFeature asyncHalibutFeature)
        {
            AsyncHalibutFeature = asyncHalibutFeature;
        }

        public AsyncHalibutFeature AsyncHalibutFeature { get; }
        public Stream CreateStream(TcpClient client)
        {
            var stream = client.GetStream();
            if (AsyncHalibutFeature.IsEnabled())
            {
                return new NetworkTimeoutStream(stream);
            }

            return stream;
        }

        public WebSocketStream CreateStream(WebSocket webSocket)
        {
            return new WebSocketStream(webSocket);
        }
    }
}