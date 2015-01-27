using System;
using System.Net.Security;
using System.Net.Sockets;
using Halibut.Transport.Protocol;

namespace Halibut.Transport
{
    public class SecureConnection : IDisposable
    {
        readonly TcpClient client;
        readonly SslStream stream;
        readonly MessageExchangeProtocol protocol;

        public SecureConnection(TcpClient client, SslStream stream, MessageExchangeProtocol protocol)
        {
            this.client = client;
            this.stream = stream;
            this.protocol = protocol;
        }

        public MessageExchangeProtocol Protocol { get { return protocol; } }
        
        public void Dispose()
        {
            stream.Dispose();
            client.Close();
        }
    }
}