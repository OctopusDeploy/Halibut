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
            //return new NetworkTimeoutStream(stream);
            return stream;
        }

        public Stream CreateStream(WebSocket webSocket)
        {
            var webSocketStream = new WebSocketStream(webSocket);
            var networkStream = new NetworkTimeoutStream(webSocketStream);

            // When synchronous serialization is performed, it will call the synchronous versions of Read/Write.
            // This means that the NetworkTimeoutStream cannot actually cause these methods to time out (as only the async versions do that).
            // Since the underlying WebSocket does not timeout itself, then that means a synchronous Read/Write will never time out.
            // To make it time out, we simply use a stream wrapper that forces synchronous Read/Write to use the async versions instead. 
            // Therefore using the async versions of NetworkTimeoutStream, and ensuring timeouts occur.
            var callUnderlyingAsyncMethodsStream = new CallUnderlyingAsyncMethodsStream(networkStream);
            
            return callUnderlyingAsyncMethodsStream;
        }
    }
}