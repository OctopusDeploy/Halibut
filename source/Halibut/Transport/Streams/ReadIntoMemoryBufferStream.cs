using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Transport.Observability;

namespace Halibut.Transport.Streams
{
    public class ReadIntoMemoryBufferStream : Stream
    {
        readonly MemoryStream memoryStream;
        readonly Stream sourceStream;
        readonly OnDispose onDispose;

        bool limitReached;

        public ReadIntoMemoryBufferStream(Stream sourceStream, OnDispose onDispose)
        {
            memoryStream = new MemoryStream();
            this.sourceStream = sourceStream;
            this.onDispose = onDispose;
        }

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

        bool ShouldReadFromMemoryStream => !limitReached || memoryStream.Position < memoryStream.Length;

        public override bool CanRead => sourceStream.CanRead;
        public override bool CanWrite => false;
        public override bool CanSeek => false;
        public override bool CanTimeout => false;

        public override long Length => throw new NotSupportedException();
        
        public override long Position
        {
            get => memoryStream.Position + sourceStream.Position;
            set
            {
                //TODO: verify this is correct.
                if (value < memoryStream.Length)
                {
                    memoryStream.Position = value;
                    sourceStream.Position = 0;
                }
                else
                {
                    memoryStream.Position = memoryStream.Length;
                    sourceStream.Position = value - memoryStream.Length;
                }
            }
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

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (ShouldReadFromMemoryStream)
            {
                return memoryStream.Read(buffer, offset, count);
            }

            return sourceStream.Read(buffer, offset, count);
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (ShouldReadFromMemoryStream)
            {
                return await memoryStream.ReadAsync(buffer, offset, count, cancellationToken);
            }

            return await sourceStream.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public async Task BufferIntoMemoryFromSourceStreamUntilLimitReached(long readIntoMemoryLimitBytes, CancellationToken cancellationToken)
        {
            var buffer = new byte[81920];
            while (memoryStream.Length < readIntoMemoryLimitBytes)
            {
                var bytesToCopy = (int)Math.Min(buffer.Length, readIntoMemoryLimitBytes - memoryStream.Length);
                var bytesRead = await sourceStream.ReadAsync(buffer, 0, bytesToCopy, cancellationToken).ConfigureAwait(false);

                if (bytesRead == 0)
                {
                    memoryStream.Position = 0;
                    return;
                }

                await memoryStream.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
            }
            
            limitReached = true;
        }
    }
}