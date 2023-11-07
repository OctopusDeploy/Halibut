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
        readonly HalibutTimeoutsAndLimits halibutTimeoutsAndLimits;
        readonly MessageExchangeProtocol protocol;
        DateTimeOffset lastUsed;

        public SecureConnection(
            IDisposable client, 
            Stream stream,
            ExchangeProtocolBuilder exchangeProtocolBuilder,
            HalibutTimeoutsAndLimits halibutTimeoutsAndLimits,
            ILog log)
        {
            this.client = client;
            this.stream = stream;
            this.halibutTimeoutsAndLimits = halibutTimeoutsAndLimits;
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
            return lastUsed < DateTimeOffset.UtcNow.Subtract(halibutTimeoutsAndLimits.SafeTcpClientPooledConnectionTimeout);
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