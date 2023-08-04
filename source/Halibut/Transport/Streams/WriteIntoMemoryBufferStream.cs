using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Transport.Observability;

namespace Halibut.Transport.Streams
{
    public class WriteIntoMemoryBufferStream : Stream
    {
        readonly MemoryStream memoryStream;
        readonly Stream sinkStream;
        readonly long writeIntoMemoryLimitBytes;
        readonly OnDispose onDispose;
        bool usingSinkStream;

        public WriteIntoMemoryBufferStream(Stream sinkStream, long writeIntoMemoryLimitBytes, OnDispose onDispose)
        {
            memoryStream = new MemoryStream();
            this.sinkStream = sinkStream;
            this.writeIntoMemoryLimitBytes = writeIntoMemoryLimitBytes;
            this.onDispose = onDispose;
        }
        
        public long BytesWrittenIntoMemory => memoryStream.Length;

        public override bool CanRead => false;
        public override bool CanWrite => sinkStream.CanWrite;
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
                    sinkStream.Dispose();
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

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (!usingSinkStream)
            {
                var remainingSpace = writeIntoMemoryLimitBytes - BytesWrittenIntoMemory;
                if (count <= remainingSpace)
                {
                    // We fit completely in memory
                    await memoryStream.WriteAsync(buffer, offset, count, cancellationToken);
                    return;
                }

                // We tried our best, but will no longer fit in memory. Transition over to use the sinkStream.
                memoryStream.Position = 0;
                await memoryStream.CopyToAsync(sinkStream, 81920, cancellationToken);
                usingSinkStream = true;
            }

            await sinkStream.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (!usingSinkStream)
            {
                var remainingSpace = writeIntoMemoryLimitBytes - BytesWrittenIntoMemory;
                if (count <= remainingSpace)
                {
                    // We fit completely in memory
                    memoryStream.Write(buffer, offset, count);
                    return;
                }

                // We tried our best, but will no longer fit in memory. Transition over to use the sinkStream.
                memoryStream.Position = 0;
                memoryStream.CopyTo(sinkStream);
                usingSinkStream = true;
            }

            sinkStream.Write(buffer, offset, count);
        }

        public async Task WriteAnyUnwrittenDataToSinkStream(CancellationToken cancellationToken)
        {
            if (!usingSinkStream)
            {
                memoryStream.Position = 0;
                await memoryStream.CopyToAsync(sinkStream, 81920, cancellationToken);
            }
        }
    }
}