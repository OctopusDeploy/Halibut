using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Transport.Streams
{
    public class ReadIntoMemoryBufferStream : AsyncStream
    {
        readonly MemoryStream memoryBuffer;
        readonly Stream sourceStream;
        readonly long readIntoMemoryLimitBytes;
        readonly OnDispose onDispose;
        
        public ReadIntoMemoryBufferStream(Stream sourceStream, long readIntoMemoryLimitBytes, OnDispose onDispose)
        {
            memoryBuffer = new MemoryStream();
            this.sourceStream = sourceStream;
            this.readIntoMemoryLimitBytes = readIntoMemoryLimitBytes;
            this.onDispose = onDispose;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                memoryBuffer.Dispose();

                if (onDispose == OnDispose.DisposeInputStream)
                {
                    sourceStream.Dispose();
                }
            }
        }

        public override async ValueTask DisposeAsync()
        {
            await memoryBuffer.DisposeAsync();

            if (onDispose == OnDispose.DisposeInputStream)
            {
                await sourceStream.DisposeAsync();
            }
        }

        bool ShouldReadFromMemoryStream
        {
            get
            {
                if (BytesReadIntoMemory == 0) return false;

                return memoryBuffer.Position < BytesReadIntoMemory;
            }
        }

        public long BytesReadIntoMemory => memoryBuffer.Length;

        public override bool CanRead => sourceStream.CanRead;
        public override bool CanWrite => false;
        public override bool CanSeek => false;
        public override bool CanTimeout => false;

        public override long Length => throw new NotSupportedException();

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
        
        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (ShouldReadFromMemoryStream)
            {
                return memoryBuffer.Read(buffer, offset, count);
            }

            return sourceStream.Read(buffer, offset, count);
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (ShouldReadFromMemoryStream)
            {
                return await memoryBuffer.ReadAsync(buffer, offset, count, cancellationToken);
            }

            return await sourceStream.ReadAsync(buffer, offset, count, cancellationToken);
        }

        

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public async Task BufferIntoMemoryFromSourceStreamUntilLimitReached(CancellationToken cancellationToken)
        {
            var buffer = new byte[8192];
            while (BytesReadIntoMemory < readIntoMemoryLimitBytes)
            {
                var bytesToCopy = (int)Math.Min(buffer.Length, readIntoMemoryLimitBytes - BytesReadIntoMemory);
                var bytesRead = await sourceStream.ReadAsync(buffer, 0, bytesToCopy, cancellationToken).ConfigureAwait(false);

                if (bytesRead == 0)
                {
                    break;
                }

                await memoryBuffer.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
            }

            memoryBuffer.Position = 0;
        }
    }
}