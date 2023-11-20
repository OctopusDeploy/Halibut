using System;
using System.IO;
using System.Threading.Tasks;
using Halibut.Diagnostics;

namespace Halibut.Transport.Protocol
{
    public static class StreamTimeoutExtensionMethods
    {
        public static async Task WithTimeout(this Stream stream, SendReceiveTimeout timeout, Func<Task> func)
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
                stream.SetReadAndWriteTimeouts(timeout);
                await func();
            }
            finally
            {
                stream.ReadTimeout = currentReadTimeout;
                stream.WriteTimeout = currentWriteTimeout;
            }
        }

        public static async Task<T> WithTimeout<T>(this Stream stream, SendReceiveTimeout timeout, Func<Task<T>> func)
        {
            if (!stream.CanTimeout)
            {
                return await func();
            }

            var currentReadTimeout = stream.ReadTimeout;
            var currentWriteTimeout = stream.WriteTimeout;

            try
            {
                stream.SetReadAndWriteTimeouts(timeout);
                return await func();
            }
            finally
            {
                stream.ReadTimeout = currentReadTimeout;
                stream.WriteTimeout = currentWriteTimeout;
            }
        }

        public static void SetReadAndWriteTimeouts(this Stream stream, SendReceiveTimeout timeout)
        {
            if (!stream.CanTimeout)
            {
                return;
            }

            stream.WriteTimeout = (int)timeout.SendTimeout.TotalMilliseconds;
            stream.ReadTimeout = (int)timeout.ReceiveTimeout.TotalMilliseconds;
        }
    }
}
