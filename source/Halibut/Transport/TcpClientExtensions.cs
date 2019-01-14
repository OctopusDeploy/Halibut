using System;
using System.Net.Sockets;
using Halibut.Diagnostics;
using System.Runtime.ExceptionServices;

namespace Halibut.Transport
{
    public static class TcpClientExtensions
    {
        public static void ConnectWithTimeout(this TcpClient client, Uri remoteUri, TimeSpan timeout)
        {
            client.ConnectWithTimeout(remoteUri.Host, remoteUri.Port, timeout);
        }

        public static void ConnectWithTimeout(this TcpClient client, string host, int port, TimeSpan timeout)
        {
            var connectResult = false;
            try
            {
                connectResult = client.ConnectAsync(host, port).Wait(timeout);
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

                throw new HalibutClientException("The client was unable to establish the initial connection within " + timeout);
            }
        }
    }
}