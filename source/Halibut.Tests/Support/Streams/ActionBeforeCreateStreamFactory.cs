using System;
using System.IO;
using System.Net.Sockets;
using System.Net.WebSockets;
using Halibut.Transport.Protocol;
using Halibut.Transport.Streams;

namespace Halibut.Tests.Support.Streams
{
    public class ActionBeforeCreateStreamFactory : IStreamFactory
    {
        IStreamFactory streamFactory;
        Action beforeCreateStream;

        public ActionBeforeCreateStreamFactory(IStreamFactory streamFactory, Action beforeCreateStream)
        {
            this.streamFactory = streamFactory;
            this.beforeCreateStream = beforeCreateStream;
        }

        public Stream CreateStream(TcpClient stream)
        {
            beforeCreateStream();
            return streamFactory.CreateStream(stream);
        }

        public WebSocketStream CreateStream(WebSocket webSocket)
        {
            beforeCreateStream();
            return streamFactory.CreateStream(webSocket);
        }
    }
}