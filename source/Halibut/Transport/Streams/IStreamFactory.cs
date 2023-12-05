using System.IO;
using System.Net.Sockets;
using System.Net.WebSockets;

namespace Halibut.Transport.Streams
{
    public interface IStreamFactory
    {
        Stream CreateStream(TcpClient stream);
        
        Stream CreateStream(WebSocket webSocket);
    }
}