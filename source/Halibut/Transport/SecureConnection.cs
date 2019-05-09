using System;
using System.IO;
using System.Net.Security;
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

        public SecureConnection(IDisposable client, Stream stream, MessageExchangeProtocol protocol)
        {
            this.client = client;
            this.stream = stream;
            this.protocol = protocol;
            lastUsed = DateTimeOffset.UtcNow;
        }

        internal MessageExchangeProtocol Protocol { get { return protocol; } }
        public event EventHandler OnDisposed;

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
            OnDisposed?.Invoke(this, EventArgs.Empty);
            try
            {
                protocol.StopAcceptingClientRequests();
                try
                {
                    protocol.EndCommunicationWithServer();
                }
                catch (IOException ioe) when ((ioe.InnerException as SocketException) != null)
                {
                    // The stream might have already disconnected, so don't worry about it.
                }
                stream.Dispose();
                ((IDisposable)client).Dispose();
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