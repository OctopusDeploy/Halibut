using System;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using Halibut.Diagnostics;

namespace Halibut.Transport.Protocol
{
    public static class TcpClientTimeoutExtensionMethods
    {
        public static async Task WithTimeout(this TcpClient stream, HalibutTimeoutsAndLimits halibutTimeoutsAndLimits, MessageExchangeStreamTimeout timeout, Func<Task> func)
        {
            var currentReadTimeout = stream.Client.ReceiveTimeout;
            var currentWriteTimeout = stream.Client.SendTimeout;

            try
            {
                stream.SetReadAndWriteTimeouts(timeout, halibutTimeoutsAndLimits);
                await func();
            }
            finally
            {
                stream.ReceiveTimeout = currentReadTimeout;
                stream.SendTimeout = currentWriteTimeout;
            }
        }

        public static void SetReadAndWriteTimeouts(this TcpClient stream, MessageExchangeStreamTimeout timeout, HalibutTimeoutsAndLimits halibutTimeoutsAndLimits)
        {
            switch (timeout)
            {
                case MessageExchangeStreamTimeout.NormalTimeout:
                    stream.Client.SendTimeout = (int)halibutTimeoutsAndLimits.TcpClientSendTimeout.TotalMilliseconds;
                    stream.Client.ReceiveTimeout = (int)halibutTimeoutsAndLimits.TcpClientReceiveTimeout.TotalMilliseconds;
                    break;
                case MessageExchangeStreamTimeout.ControlMessageExchangeShortTimeout:
                    stream.Client.SendTimeout = (int)halibutTimeoutsAndLimits.TcpClientHeartbeatSendTimeout.TotalMilliseconds;
                    stream.Client.ReceiveTimeout = (int)halibutTimeoutsAndLimits.TcpClientHeartbeatReceiveTimeout.TotalMilliseconds;
                    break;
                case MessageExchangeStreamTimeout.AuthenticationShortTimeout:
                    stream.Client.SendTimeout = (int)halibutTimeoutsAndLimits.TcpClientAuthenticationSendTimeout.TotalMilliseconds;
                    stream.Client.ReceiveTimeout = (int)halibutTimeoutsAndLimits.TcpClientAuthenticationReceiveTimeout.TotalMilliseconds;
                    break;
                case MessageExchangeStreamTimeout.PollingForNextRequestShortTimeout:
                    stream.Client.SendTimeout = (int)halibutTimeoutsAndLimits.TcpClientPollingForNextRequestSendTimeout.TotalMilliseconds;
                    stream.Client.ReceiveTimeout = (int)halibutTimeoutsAndLimits.TcpClientPollingForNextRequestReceiveTimeout.TotalMilliseconds;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(timeout), timeout, null);
            }
        }
    }
}
