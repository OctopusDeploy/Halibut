using System;
using System.Net.Sockets;
using Halibut.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

namespace Halibut.Transport
{
    public static class TcpClientExtensions
    {
        public static Task ConnectWithTimeout(this TcpClient client, Uri remoteUri, TimeSpan timeout)
        {
            return client.ConnectWithTimeout(remoteUri.Host, remoteUri.Port, timeout);
        }

        public static async Task ConnectWithTimeout(this TcpClient client, string host, int port, TimeSpan timeout)
        {
            var connectResult = false;

            var timeoutTask = Task.Delay(timeout);
            await Task.WhenAny(client.ConnectAsync(host, port), timeoutTask)
                .ContinueWith(t =>
                {
                    var finishedTask = t.Result;
                    if (!finishedTask.Equals(timeoutTask))
                    {
                        if (finishedTask.IsFaulted)
                        {
                            var aex = finishedTask.Exception;
                            if (aex != null)
                            {
                                // if timeout is > 20 seconds the underlying socket will timeout first
                                if (!aex.IsSocketConnectionTimeout())
                                {
                                    ExceptionDispatchInfo.Capture(aex.InnerException).Throw();
                                }
                            }

                            return;
                        }

                        connectResult = true;
                    }
                })
                .ConfigureAwait(false);

            if (!connectResult)
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

                throw new HalibutClientException("The client was unable to establish the initial connection within " + HalibutLimits.TcpClientConnectTimeout);
            }
        }
    }
}