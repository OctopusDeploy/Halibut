using System;
using System.IO;
using System.Net.Sockets;
using Halibut.Diagnostics;
using Halibut.Transport.Protocol;
using Halibut.Util;

namespace Halibut.Transport
{
    public class SecureConnection : IConnection
    {
        readonly IDisposable client;
        readonly Stream stream;
        readonly AsyncHalibutFeature asyncHalibutFeature;
        readonly HalibutTimeoutsAndLimits halibutTimeoutsAndLimits;
        readonly MessageExchangeProtocol protocol;
        DateTimeOffset lastUsed;

        public SecureConnection(IDisposable client, 
            Stream stream,
            ExchangeProtocolBuilder exchangeProtocolBuilder,
            AsyncHalibutFeature asyncHalibutFeature,
            HalibutTimeoutsAndLimits halibutTimeoutsAndLimits,
            ILog log)
        {
            this.client = client;
            this.stream = stream;
            this.asyncHalibutFeature = asyncHalibutFeature;
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
            if (asyncHalibutFeature.IsEnabled())
            {
                return lastUsed < DateTimeOffset.UtcNow.Subtract(halibutTimeoutsAndLimits.SafeTcpClientPooledConnectionTimeout);
            }
#pragma warning disable CS0612
            return lastUsed < DateTimeOffset.UtcNow.Subtract(HalibutLimits.SafeTcpClientPooledConnectionTimeout);
#pragma warning restore CS0612
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