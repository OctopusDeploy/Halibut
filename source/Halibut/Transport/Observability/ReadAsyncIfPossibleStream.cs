using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System;

namespace Halibut.Transport.Observability
{
    public class ReadAsyncIfPossibleStream : Stream
    {
        readonly MemoryStream memoryStream;
        readonly Stream sourceStream;
        readonly long readIntoMemoryLimitBytes;
        readonly OnDispose onDispose;
        
        bool limitReached;

        public ReadAsyncIfPossibleStream(Stream sourceStream, long readIntoMemoryLimitBytes, OnDispose onDispose)
        {
            memoryStream = new MemoryStream();
            this.sourceStream = sourceStream;
            this.readIntoMemoryLimitBytes = readIntoMemoryLimitBytes;
            this.onDispose = onDispose;
        }
        
        public override bool CanRead => sourceStream.CanRead;
        public override bool CanWrite => false;
        public override bool CanSeek => false;
        public override bool CanTimeout => false;

        public override long Length => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                memoryStream.Dispose();

                if (onDispose == OnDispose.DisposeInputStream)
                {
                    sourceStream.Dispose();
                }
            }
        }

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int ReadTimeout
        {
            get => sourceStream.ReadTimeout;
            set => throw new NotSupportedException();
        }

        public override int WriteTimeout
        {
            get => sourceStream.WriteTimeout;
            set => throw new NotSupportedException();
        }
        
        public override void Flush() => throw new NotSupportedException();
        
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (!limitReached || memoryStream.Position < memoryStream.Length)
            {
                return await memoryStream.ReadAsync(buffer, offset, count, cancellationToken);
            }

            return await sourceStream.ReadAsync(buffer, offset, count, cancellationToken);
        }
        
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (!limitReached || memoryStream.Position < memoryStream.Length)
            {
                return memoryStream.Read(buffer, offset, count);
            }

            return sourceStream.Read(buffer, offset, count);
        }
        
        public async Task BufferFromSourceStreamUntilLimitReached(CancellationToken cancellationToken)
        {
            var totalBytesRead = 0L;
            var buffer = new byte[81920];
            while (totalBytesRead < readIntoMemoryLimitBytes)
            {
                var bytesToCopy = (int)Math.Min(buffer.Length, readIntoMemoryLimitBytes - totalBytesRead);
                var bytesRead = await sourceStream.ReadAsync(buffer, 0, bytesToCopy, cancellationToken).ConfigureAwait(false);

                if (bytesRead == 0)
                {
                    memoryStream.Position = 0;
                    return;
                }

                await memoryStream.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
                totalBytesRead += bytesRead;
            }

            memoryStream.Position = 0;
            limitReached = true;
        }
    }
}