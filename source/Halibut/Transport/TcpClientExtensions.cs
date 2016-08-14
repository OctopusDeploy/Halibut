using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using Halibut.Diagnostics;

namespace Halibut.Transport
{
    public static class TcpClientExtensions
    {
        public static async Task ConnectWithTimeout(this TcpClient client, Uri remoteUri, TimeSpan timeout)
        {
            await client.ConnectWithTimeout(remoteUri.Host, remoteUri.Port, timeout);
        }

        public static async Task ConnectWithTimeout(this TcpClient client, string host, int port, TimeSpan timeout)
        {
            var connectTask = client.ConnectAsync(host, port);
            var timeoutTask = Task.Delay(timeout);
            var finishedFirst = await Task.WhenAny(connectTask, timeoutTask);
            if (finishedFirst == timeoutTask)
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

                throw new HalibutClientException("The client was unable to establish the initial connection within " + HalibutLimits.TcpClientConnectTimeout);
            }

            // await the connect task to throw any connection exceptions
            await connectTask;
        }
    }
}