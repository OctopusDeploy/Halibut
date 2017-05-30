using System;
using System.IO;
using System.Threading;
using Halibut.Diagnostics;
using Halibut.Transport.Protocol;

namespace Halibut.Transport
{
    public class SecureConnection : IConnection
    {
        IDisposable client;
        Stream stream;
        readonly MessageExchangeProtocol protocol;
        DateTimeOffset lastUsed;

        public SecureConnection(IDisposable client, Stream stream, MessageExchangeProtocol protocol)
        {
            this.client = client;
            this.stream = stream;
            this.protocol = protocol;
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
            // Injected by Janitor
        }
    }
}