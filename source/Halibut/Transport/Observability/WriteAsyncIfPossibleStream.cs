using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System;

namespace Halibut.Transport.Observability
{
    public class WriteAsyncIfPossibleStream : Stream
    {
        readonly MemoryStream memoryStream;
        readonly Stream sinkStream;
        readonly long readIntoMemoryLimitBytes;
        readonly OnDispose onDispose;

        bool usingSinkStream;

        public WriteAsyncIfPossibleStream(Stream sinkStream, long readIntoMemoryLimitBytes, OnDispose onDispose)
        {
            memoryStream = new MemoryStream();
            this.sinkStream = sinkStream;
            this.readIntoMemoryLimitBytes = readIntoMemoryLimitBytes;
            this.onDispose = onDispose;
        }
        
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
            get => sinkStream.ReadTimeout;
            set => throw new NotSupportedException();
        }

        public override int WriteTimeout
        {
            get => sinkStream.WriteTimeout;
            set => throw new NotSupportedException();
        }

        public override void Flush() => throw new NotSupportedException();
        
        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            // No need to go via in memory here, as this is already async.
            await sinkStream.WriteAsync(buffer, offset, count, cancellationToken);
        }
        
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (!usingSinkStream)
            {
                var remainingSpace = readIntoMemoryLimitBytes - memoryStream.Length;
                if (count < remainingSpace)
                {
                    // We fit in memory
                    memoryStream.Write(buffer, offset, count);
                    return;
                }

                //We will no longer fit in memory. Transition over to use the sinkStream.
                memoryStream.Position = 0;
                memoryStream.CopyTo(sinkStream);
                usingSinkStream = true;
            }

            sinkStream.Write(buffer, offset, count);
        }

        public async Task WriteAnyUnwrittenDataToWrappedStream(CancellationToken cancellationToken)
        {
            if (!usingSinkStream)
            {
                memoryStream.Position = 0;
                await memoryStream.CopyToAsync(sinkStream, 81920, cancellationToken);
            }
        }
    }
}