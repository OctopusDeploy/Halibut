using System;
using System.IO;
using System.Net.Sockets;
using System.Net.WebSockets;
using Halibut.Transport.Streams;

namespace Halibut.Tests.Support.Streams
{
    public class StreamWrappingStreamFactory :  IStreamFactory
    {
        public Func<Stream, Stream> WrapStreamWith = s => s;
        
        public Stream CreateStream(TcpClient stream)
        {
            return WrapStreamWith(new StreamFactory().CreateStream(stream));
        }

        public Stream CreateStream(WebSocket webSocket)
        {
            return new StreamFactory().CreateStream(webSocket);
        }
    }
}