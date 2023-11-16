using System;
using System.IO;
using System.Threading.Tasks;
using Halibut.Diagnostics;

namespace Halibut.Transport.Protocol
{
    public static class StreamTimeoutExtensionMethods
    {
        public static async Task WithTimeout(this Stream stream, HalibutTimeoutsAndLimits halibutTimeoutsAndLimits, MessageExchangeStreamTimeout timeout, Func<Task> func)
        {
            if (!stream.CanTimeout)
            {
                await func();

                return;
            }

            var currentReadTimeout = stream.ReadTimeout;
            var currentWriteTimeout = stream.WriteTimeout;

            try
            {
                stream.SetReadAndWriteTimeouts(timeout, halibutTimeoutsAndLimits);
                await func();
            }
            finally
            {
                stream.ReadTimeout = currentReadTimeout;
                stream.WriteTimeout = currentWriteTimeout;
            }
        }

        public static async Task<T> WithTimeout<T>(this Stream stream, HalibutTimeoutsAndLimits halibutTimeoutsAndLimits, MessageExchangeStreamTimeout timeout, Func<Task<T>> func)
        {
            if (!stream.CanTimeout)
            {
                return await func();
            }

            var currentReadTimeout = stream.ReadTimeout;
            var currentWriteTimeout = stream.WriteTimeout;

            try
            {
                stream.SetReadAndWriteTimeouts(timeout, halibutTimeoutsAndLimits);
                return await func();
            }
            finally
            {
                stream.ReadTimeout = currentReadTimeout;
                stream.WriteTimeout = currentWriteTimeout;
            }
        }

        public static void SetReadAndWriteTimeouts(this Stream stream, MessageExchangeStreamTimeout timeout, HalibutTimeoutsAndLimits halibutTimeoutsAndLimits)
        {
            if (!stream.CanTimeout)
            {
                return;
            }

            switch (timeout)
            {
                case MessageExchangeStreamTimeout.NormalTimeout:
                    stream.WriteTimeout = (int)halibutTimeoutsAndLimits.TcpClientSendTimeout.TotalMilliseconds;
                    stream.ReadTimeout = (int)halibutTimeoutsAndLimits.TcpClientReceiveTimeout.TotalMilliseconds;
                    break;
                case MessageExchangeStreamTimeout.ControlMessageExchangeShortTimeout:
                    stream.WriteTimeout = (int)halibutTimeoutsAndLimits.TcpClientHeartbeatSendTimeout.TotalMilliseconds;
                    stream.ReadTimeout = (int)halibutTimeoutsAndLimits.TcpClientHeartbeatReceiveTimeout.TotalMilliseconds;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(timeout), timeout, null);
            }
        }
    }
}
