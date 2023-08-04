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

        public SecureConnection(IDisposable client, Stream stream, ExchangeProtocolBuilder exchangeProtocolBuilder, ILog log)
        {
            this.client = client;
            this.stream = stream;
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
            return lastUsed < DateTimeOffset.UtcNow.Subtract(HalibutLimits.SafeTcpClientPooledConnectionTimeout);
        }
        
        public void Dispose()
        {
            try
            {
                protocol.StopAcceptingClientRequests();
                try
                {
                    // TODO - ASYNC ME UP!
#pragma warning disable CS0612
                    protocol.EndCommunicationWithServer();
#pragma warning restore CS0612
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