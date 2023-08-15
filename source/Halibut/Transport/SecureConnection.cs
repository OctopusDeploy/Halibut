using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.Transport.Protocol;
using Halibut.Transport.Streams;

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

        public async ValueTask DisposeAsync()
        {
            try
            {
                protocol.StopAcceptingClientRequests();
                try
                {
                    await protocol.EndCommunicationWithServerAsync(CancellationToken.None);
                }
                catch (Exception)
                {
                    // The stream might have already disconnected, so don't worry about it.
                }
                await stream.DisposeAsync();
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