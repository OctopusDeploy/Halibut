using System;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using Halibut.Tests.Transport.Streams;

namespace Halibut.Tests.Util
{
    public static class StreamExtensionMethods
    {
        public static void WriteString(this Stream stream, string s)
        {
            var bytes = s.GetBytesUtf8();
            stream.Write(bytes, 0, bytes.Length);
        }

        public static async Task<int> ReadFromStream(this Stream sut, StreamMethod streamMethod, byte[] readBuffer, int offset, int count, CancellationToken cancellationToken)
        {
            switch (streamMethod)
            {
                case StreamMethod.Async:
                    return await sut.ReadAsync(readBuffer, offset, count, cancellationToken);
                case StreamMethod.Sync:
                    return sut.Read(readBuffer, offset, count);
                case StreamMethod.LegacyAsync:
                    return await sut.ReadFromStreamLegacyAsync(readBuffer, offset, count, cancellationToken);
                default:
                    throw new ArgumentOutOfRangeException(nameof(streamMethod), streamMethod, null);
            }
        }

        public static async Task<int> ReadFromStreamLegacyAsync(this Stream sut, byte[] readBuffer, int offset, int count, CancellationToken cancellationToken)
        {
            // This is the way async reading was done in earlier version of .NET
            var bytesRead = -1;
            sut.BeginRead(readBuffer, offset, count, AsyncCallback, sut);

            Exception? exception = null;
            void AsyncCallback(IAsyncResult result)
            {
                try
                {
                    bytesRead = sut.EndRead(result);
                }
                catch (Exception e)
                {
                    exception = e;
                    throw;
                }
            }

            while (bytesRead < 0 && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(10, cancellationToken);

                if (exception is not null)
                {
                    throw exception;
                }
            }

            return bytesRead;
        }

        public static async Task WriteToStream(this Stream sut, StreamMethod streamMethod, byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            switch (streamMethod)
            {
                case StreamMethod.Async:
                    await sut.WriteAsync(buffer, offset, count, cancellationToken);
                    return;
                case StreamMethod.Sync:
                    sut.Write(buffer, offset, count);
                    return;
                case StreamMethod.LegacyAsync:
                    await WriteToStreamLegacyAsync(sut, buffer, offset, count, cancellationToken);
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(streamMethod), streamMethod, null);
            }
        }

        static async Task WriteToStreamLegacyAsync(Stream sut, byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            // This is the way async writing was done in earlier version of .NET
            var written = false;
            sut.BeginWrite(buffer, offset, count, AsyncCallback, sut);

            Exception? exception = null;
            void AsyncCallback(IAsyncResult result)
            {
                try
                {
                    sut.EndWrite(result);
                    written = true;
                }
                catch (Exception e)
                {
                    exception = e;
                    throw;
                }
            }

            while (!written && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(10, cancellationToken);

                if (exception is not null)
                {
                    throw exception;
                }
            }
        }
    }
}