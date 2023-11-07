using System;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Util;
using Halibut.Util.AsyncEx;

namespace Halibut.Transport
{
    public static class TcpClientExtensions
    {
        public static async Task ConnectWithTimeoutAsync(this TcpClient client, Uri remoteUri, TimeSpan timeout, CancellationToken cancellationToken)
        {
            await ConnectWithTimeoutAsync(client, remoteUri.Host, remoteUri.Port, timeout, cancellationToken);
        }
        
        public static async Task ConnectWithTimeoutAsync(this TcpClient client, string host, int port, TimeSpan timeout, CancellationToken cancellationToken)
        {
            try
            {
                var task = client.ConnectAsync(host, port);
#pragma warning disable CS0612 // Type or member is obsolete
                await task.TimeoutAfter(timeout, cancellationToken);
#pragma warning restore CS0612 // Type or member is obsolete
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

        public static void CloseImmediately(this TcpClient client)
        {
            client.CloseImmediately(_ => { });
        }

        public static void CloseImmediately(this TcpClient client, Action<Exception> onError)
        {
            Try.CatchingError(() => client.Client.Close(0), onError);
            Try.CatchingError(client.Close, onError);
        }

        public static string GetRemoteEndpointString(this TcpClient client)
        {
            try
            {
                return client?.Client.RemoteEndPoint?.ToString() ?? "<none>";
            }
            catch
            {
                return "<error>";
            }
        }
    }
}
