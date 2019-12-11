using System;
using System.Net.Sockets;
using Halibut.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace Halibut.Transport
{
    public static class TcpClientExtensions
    {
        [Obsolete]
        public static void ConnectWithTimeout(this TcpClient client, Uri remoteUri, TimeSpan timeout)
        {
            ConnectWithTimeout(client, remoteUri.Host, remoteUri.Port, timeout, CancellationToken.None);
        }

        public static void ConnectWithTimeout(this TcpClient client, Uri remoteUri, TimeSpan timeout, CancellationToken cancellationToken)
        {
            client.ConnectWithTimeout(remoteUri.Host, remoteUri.Port, timeout, cancellationToken);
        }

        [Obsolete]
        public static void ConnectWithTimeout(this TcpClient client, string host, int port, TimeSpan timeout)
        {
            ConnectWithTimeout(client, host, port, timeout, CancellationToken.None);
        }

        public static void ConnectWithTimeout(this TcpClient client, string host, int port, TimeSpan timeout, CancellationToken cancellationToken)
        {
            var connectResult = false;
            try
            {
                connectResult = client.ConnectAsync(host, port).Wait((int)timeout.TotalMilliseconds, cancellationToken);
            }
            catch (AggregateException aex) when (aex.IsSocketConnectionTimeout())
            {
                // if timeout is > 20 seconds the underlying socket will timeout first
            }
            catch(AggregateException aex)
            {
                ExceptionDispatchInfo.Capture(aex.InnerException).Throw();
            }
            if (!connectResult)
            {
                try
                {
                    ((IDisposable)client).Dispose();
                }
                catch (SocketException)
                {
                }
                catch (ObjectDisposedException)
                {
                }

                throw new HalibutClientException($"The client was unable to establish the initial connection within {timeout}.");
            }
        }
    }
}