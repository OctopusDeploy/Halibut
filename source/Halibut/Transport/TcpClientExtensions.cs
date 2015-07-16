using System;
using System.Net.Sockets;
using Halibut.Diagnostics;

namespace Halibut.Transport
{
    public static class TcpClientExtensions
    {
        public static void ConnectWithTimeout(this TcpClient client, Uri remoteUri, TimeSpan timeout)
        {
            var connectResult = client.BeginConnect(remoteUri.Host, remoteUri.Port, ar => { }, null);
            if (!connectResult.AsyncWaitHandle.WaitOne(HalibutLimits.TcpClientConnectTimeout))
            {
                try
                {
                    client.Close();
                }
                catch (SocketException)
                {
                }
                catch (ObjectDisposedException)
                {
                }

                throw new HalibutClientException("The client was unable to establish the initial connection within " + HalibutLimits.TcpClientConnectTimeout);
            }

            client.EndConnect(connectResult);
        }
    }
}