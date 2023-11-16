using System;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Util;
using Halibut.Diagnostics;
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
                    //client.CloseImmediately();
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

        /// <summary>
        /// Enable KeepAlive fot the TcpClient
        /// </summary>
        internal static void EnableTcpKeepAlive(this TcpClient tcpClient, HalibutTimeoutsAndLimits halibutTimeoutsAndLimits)
        {
            if (!halibutTimeoutsAndLimits.TcpKeepAliveEnabled)
            {
                return;
            }

            SetKeepAliveValues(
                tcpClient.Client,
                halibutTimeoutsAndLimits.TcpKeepAliveTime,
                halibutTimeoutsAndLimits.TcpKeepAliveInterval,
                halibutTimeoutsAndLimits.TcpKeepAliveRetryCount);
        }

        /// <summary>
        /// Configure KeepAliveValues for Socket
        /// </summary>
        /// <param name="socket">Socket</param>
        /// <param name="keepAliveTime">The duration a TCP connection will remain alive/idle before keepalive probes are sent to the remote.</param>
        /// <param name="keepAliveInterval">The duration a TCP connection will wait for a keepalive response before sending another keepalive probe.</param>
        /// <param name="tcpKeepAliveRetryCount">The number of TCP keep alive probes that will be sent before the connection is terminated.</param>
        static void SetKeepAliveValues(Socket socket, TimeSpan keepAliveTime, TimeSpan keepAliveInterval, int tcpKeepAliveRetryCount)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
#if HAS_TCP_KEEP_ALIVE_SOCKET_OPTIONS
                try
                {
                    // SetSocketOptions are not supported on older versions of Windows
                    SetSocketOptions();
                }
                catch
                {
                    // Fallback to IOControl to set keep alive settings
                    SetIoControl();
                }
#else
                SetIoControl();
#endif

                // Supported on net48 and net6.0 on Windows
                void SetIoControl()
                {
                    var size = Marshal.SizeOf((uint)0);
                    var optionInValue = new byte[size * 3];

                    BitConverter.GetBytes((uint)(1)).CopyTo(optionInValue, 0);
                    BitConverter.GetBytes((uint)keepAliveTime.TotalMilliseconds).CopyTo(optionInValue, size);
                    BitConverter.GetBytes((uint)keepAliveInterval.TotalMilliseconds).CopyTo(optionInValue, size * 2);

                    socket.IOControl(IOControlCode.KeepAliveValues, optionInValue, null);
                }
            }
            else
            {
#if HAS_TCP_KEEP_ALIVE_SOCKET_OPTIONS
                SetSocketOptions();
#endif
            }

#if HAS_TCP_KEEP_ALIVE_SOCKET_OPTIONS
            void SetSocketOptions()
            {
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, (int)keepAliveTime.TotalSeconds);
                socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, (int)keepAliveInterval.TotalSeconds);
                socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, tcpKeepAliveRetryCount);
            }
#endif
        }
    }
}
