using System.IO;
using System.Net.Sockets;
using System.Net.WebSockets;
using Halibut.Transport.Protocol;

namespace Halibut.Transport.Streams
{
    public interface IStreamFactory
    {
        Stream CreateStream(TcpClient stream);
        
        WebSocketStream CreateStream(WebSocket webSocket);
    }
}