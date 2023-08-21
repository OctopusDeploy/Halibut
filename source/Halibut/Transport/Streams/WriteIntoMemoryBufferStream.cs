using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Transport.Streams
{
    public class WriteIntoMemoryBufferStream : AsyncStream
    {
        readonly MemoryStream memoryBuffer;
        readonly Stream innerStream;
        readonly long writeIntoMemoryLimitBytes;
        readonly OnDispose onDispose;
        bool usingMemoryBuffer = true;

        public WriteIntoMemoryBufferStream(Stream innerStream, long writeIntoMemoryLimitBytes, OnDispose onDispose)
        {
            memoryBuffer = new MemoryStream();
            this.innerStream = innerStream;
            this.writeIntoMemoryLimitBytes = writeIntoMemoryLimitBytes;
            this.onDispose = onDispose;
        }
        
        public long BytesWrittenIntoMemory => memoryBuffer.Length;

        public override bool CanRead => false;
        public override bool CanWrite => innerStream.CanWrite;
        public override bool CanSeek => false;
        public override bool CanTimeout => false;

        public override long Length => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (usingMemoryBuffer)
                {
                    memoryBuffer.Position = 0;
                    memoryBuffer.CopyTo(innerStream);
                }
            }

            base.Dispose(disposing);

            if (disposing)
            {
                memoryBuffer.Dispose();

                if (onDispose == OnDispose.DisposeInputStream)
                {
                    innerStream.Dispose();
                }
            }
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return innerStream.FlushAsync(cancellationToken);
        }

        public override async ValueTask DisposeAsync()
        {
            if (usingMemoryBuffer)
            {
                memoryBuffer.Position = 0;
                await memoryBuffer.CopyToAsync(innerStream);
            }

            await memoryBuffer.DisposeAsync();

            if (onDispose == OnDispose.DisposeInputStream)
            {
                await innerStream.DisposeAsync();
            }
        }

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int ReadTimeout
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int WriteTimeout
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (usingMemoryBuffer)
            {
                var remainingSpace = writeIntoMemoryLimitBytes - BytesWrittenIntoMemory;
                if (count <= remainingSpace)
                {
                    // We fit completely in memory
                    await memoryBuffer.WriteAsync(buffer, offset, count, cancellationToken);
                    return;
                }

                // We tried our best, but will no longer fit in memory. Transition over to use the sinkStream.
                memoryBuffer.Position = 0;
                await memoryBuffer.CopyToAsync(innerStream, 8192, cancellationToken);
                usingMemoryBuffer = false;
            }

            await innerStream.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (usingMemoryBuffer)
            {
                var remainingSpace = writeIntoMemoryLimitBytes - BytesWrittenIntoMemory;
                if (count <= remainingSpace)
                {
                    // We fit completely in memory
                    memoryBuffer.Write(buffer, offset, count);
                    return;
                }

                // We tried our best, but will no longer fit in memory. Transition over to use the sinkStream.
                memoryBuffer.Position = 0;
                memoryBuffer.CopyTo(innerStream);
                usingMemoryBuffer = false;
            }

            innerStream.Write(buffer, offset, count);
        }

        public async Task WriteBufferToUnderlyingStream(CancellationToken cancellationToken)
        {
            if (usingMemoryBuffer)
            {
                memoryBuffer.Position = 0;
                await memoryBuffer.CopyToAsync(innerStream, 8192, cancellationToken);

                usingMemoryBuffer = false;
            }
        }
    }
}