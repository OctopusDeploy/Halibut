using System;
using System.IO;
using System.Threading.Tasks;
using Halibut.Diagnostics;

namespace Halibut.Transport.Protocol
{
    public static class StreamTimeoutExtensionMethods
    {
        public static async Task WithTimeout(this Stream stream, SendReceiveTimeout? timeout, Func<Task> func)
        {
            if (!stream.CanTimeout)
            {
                await func();

                return;
            }

            var currentReadTimeout = stream.ReadTimeout;
            var currentWriteTimeout = stream.WriteTimeout;
            var timeoutsReverted = false;

            try
            {
                stream.SetReadAndWriteTimeouts(timeout);
                await func();
            }
            finally
            {
                try
                {
                    stream.ReadTimeout = currentReadTimeout;
                    stream.WriteTimeout = currentWriteTimeout;
                    timeoutsReverted = true;
                }
                catch
                {
                }
            }

            if (!timeoutsReverted)
            {
                throw new InvalidOperationException("Could not revert the Timeouts. This should not happen.");
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
            var timeoutsReverted = false;
            T result;

            try
            {
                stream.SetReadAndWriteTimeouts(timeout);
                result = await func();
            }
            finally
            {
                try
                {
                    stream.ReadTimeout = currentReadTimeout;
                    stream.WriteTimeout = currentWriteTimeout;
                    timeoutsReverted = true;
                }
                catch
                {
                }
            }

            if (!timeoutsReverted)
            {
                throw new InvalidOperationException("Could not revert the Timeouts. This should not happen.");
            }

            return result;
        }

        public static async Task WithReadTimeout(this Stream stream, TimeSpan timeout, Func<Task> func)
        {
            if (!stream.CanTimeout)
            {
                await func();

                return;
            }

            var currentReadTimeout = stream.ReadTimeout;
            var timeoutsReverted = false;

            try
            {
                stream.SetReadTimeouts(timeout);
                await func();
            }
            finally
            {
                try
                {
                    stream.ReadTimeout = currentReadTimeout;
                    timeoutsReverted = true;
                }
                catch
                {
                }
            }

            if (!timeoutsReverted)
            {
                throw new InvalidOperationException("Could not revert the Timeouts. This should not happen.");
            }
        }

        public static async Task<T> WithReadTimeout<T>(this Stream stream, TimeSpan timeout, Func<Task<T>> func)
        {
            if (!stream.CanTimeout)
            {
                return await func();
            }

            var currentReadTimeout = stream.ReadTimeout;
            var timeoutsReverted = false;
            T result;

            try
            {
                stream.SetReadTimeouts(timeout);
                result = await func();
            }
            finally
            {
                try
                {
                    stream.ReadTimeout = currentReadTimeout;
                    timeoutsReverted = true;
                }
                catch
                {
                }
            }

            if (!timeoutsReverted)
            {
                throw new InvalidOperationException("Could not revert the Timeouts. This should not happen.");
            }

            return result;
        }

        public static void SetReadAndWriteTimeouts(this Stream stream, SendReceiveTimeout? timeout)
        {
            if (timeout == null) return;
            
            if (!stream.CanTimeout)
            {
                return;
            }

            stream.WriteTimeout = (int)timeout.SendTimeout.TotalMilliseconds;
            stream.ReadTimeout = (int)timeout.ReceiveTimeout.TotalMilliseconds;
        }
        
        public static SendReceiveTimeout? GetReadAndWriteTimeouts(this Stream stream)
        {
            if (!stream.CanTimeout)
            {
                return null;
            }

            return new SendReceiveTimeout(sendTimeout: TimeSpan.FromMilliseconds(stream.WriteTimeout), receiveTimeout: TimeSpan.FromMilliseconds(stream.ReadTimeout));
        }

        public static void SetReadTimeouts(this Stream stream, TimeSpan timeout)
        {
            if (!stream.CanTimeout)
            {
                return;
            }

            stream.ReadTimeout = (int)timeout.TotalMilliseconds;
        }
    }
}
