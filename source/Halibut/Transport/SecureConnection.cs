using System;
using System.IO;
using System.Net.Sockets;
using Halibut.Diagnostics;
using Halibut.Transport.Protocol;

namespace Halibut.Transport
{
    public class SecureConnection : IConnection
    {
        readonly IDisposable client;
        readonly Stream stream;
        readonly MessageExchangeProtocol protocol;
        DateTimeOffset lastUsed;
        readonly HalibutTimeouts halibutTimeouts;

        public SecureConnection(IDisposable client, Stream stream, ExchangeProtocolBuilder exchangeProtocolBuilder, HalibutTimeouts halibutTimeouts, ILog log)
        {
            this.client = client;
            this.stream = stream;
            this.halibutTimeouts = halibutTimeouts;
            protocol = exchangeProtocolBuilder(stream, log);
            lastUsed = DateTimeOffset.UtcNow;
        }

        public MessageExchangeProtocol Protocol => protocol;

        public void NotifyUsed()
        {
            lastUsed = DateTimeOffset.UtcNow;
        }

        public bool HasExpired()
        {
            return lastUsed < DateTimeOffset.UtcNow.Subtract(HalibutLimits.SafeTcpClientPooledConnectionTimeout(halibutTimeouts));
        }

        public void Dispose()
        {
            try
            {
                protocol.StopAcceptingClientRequests();
                try
                {
                    protocol.EndCommunicationWithServer();
                }
                catch (Exception)
                {
                    // The stream might have already disconnected, so don't worry about it.
                }
                stream.Dispose();
                client.Dispose();
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