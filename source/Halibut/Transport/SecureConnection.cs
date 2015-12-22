using System;
using System.Net.Security;
using System.Net.Sockets;
using Halibut.Diagnostics;
using Halibut.Transport.Protocol;

namespace Halibut.Transport
{
    public class SecureConnection : IConnection
    {
        readonly TcpClient client;
        readonly SslStream stream;
        readonly MessageExchangeProtocol protocol;
        DateTimeOffset lastUsed;

        public SecureConnection(TcpClient client, SslStream stream, MessageExchangeProtocol protocol)
        {
            this.client = client;
            this.stream = stream;
            this.protocol = protocol;
            lastUsed = DateTimeOffset.UtcNow;
        }

        public MessageExchangeProtocol Protocol { get { return protocol; } }

        public void NotifyUsed()
        {
            lastUsed = DateTimeOffset.UtcNow;
        }

        public bool HasExpired()
        {
            return lastUsed < DateTimeOffset.UtcNow.Subtract(HalibutLimits.TcpClientPooledConnectionTimeout);
        }

        public void Dispose()
        {
            try
            {
                stream.Dispose();
                client.Close();
            }
            catch (SocketException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }
}