using System;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Util.AsyncEx;

namespace Halibut.Transport
{
    public static class TcpClientExtensions
    {
        public static void ConnectWithTimeout(this TcpClient client, Uri remoteUri, TimeSpan timeout)
        {
            ConnectWithTimeout(client, remoteUri.Host, remoteUri.Port, timeout, CancellationToken.None);
        }

        public static void ConnectWithTimeout(this TcpClient client, Uri remoteUri, TimeSpan timeout, CancellationToken cancellationToken)
        {
            ConnectWithTimeout(client, remoteUri.Host, remoteUri.Port, timeout, cancellationToken);
        }

        public static void ConnectWithTimeout(this TcpClient client, string host, int port, TimeSpan timeout)
        {
            ConnectWithTimeout(client, host, port, timeout, CancellationToken.None);
        }

        public static void ConnectWithTimeout(this TcpClient client, string host, int port, TimeSpan timeout, CancellationToken cancellationToken)
        {
            Connect(client, host, port, timeout, cancellationToken).GetAwaiter().GetResult();
        }

        static async Task Connect(TcpClient client, string host, int port, TimeSpan timeout, CancellationToken cancellationToken)
        {
            try
            {
                var task = client.ConnectAsync(host, port);
                await task.TimeoutAfter(timeout, cancellationToken);
            }
            catch (TimeoutException)
            {
                DisposeClient();
                throw new HalibutClientException($"The client was unable to establish the initial connection within the timeout {timeout}.");
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
            {
                DisposeClient();
                throw new HalibutClientException($"The client was unable to establish the initial connection within {timeout}.");
            }
            catch (Exception ex)
            {
                DisposeClient();
                ExceptionDispatchInfo.Capture(ex).Throw();
            }
            void DisposeClient()
            {
                try
                {
                    ((IDisposable) client).Dispose();
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
}