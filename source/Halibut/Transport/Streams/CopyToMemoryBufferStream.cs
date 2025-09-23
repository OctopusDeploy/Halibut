using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Transport.Streams
{
    public class CopyToMemoryBufferStream : AsyncStream
    {
        public readonly MemoryStream memoryBuffer;
        readonly Stream sourceStream;
        readonly OnDispose onDispose;
        
        public CopyToMemoryBufferStream(Stream sourceStream, OnDispose onDispose)
        {
            memoryBuffer = new MemoryStream();
            this.sourceStream = sourceStream;
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

        public long BytesCopiedToMemory => memoryBuffer.Length;
        
        /// <summary>
        /// Gets a copy of all bytes that have been read from the source stream
        /// </summary>
        public byte[] GetCopiedBytes() => memoryBuffer.ToArray();

        public override bool CanRead => sourceStream.CanRead;
        public override bool CanWrite => false;
        public override bool CanSeek => false;
        public override bool CanTimeout => sourceStream.CanTimeout;

        public override long Length => sourceStream.Length;

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int ReadTimeout
        {
            get => sourceStream.ReadTimeout;
            set => sourceStream.ReadTimeout = value;
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
            var bytesRead = sourceStream.Read(buffer, offset, count);
            
            if (bytesRead > 0)
            {
                // Copy the read bytes to our memory buffer
                memoryBuffer.Write(buffer, offset, bytesRead);
            }
            
            return bytesRead;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var bytesRead = await sourceStream.ReadAsync(buffer, offset, count, cancellationToken);
            
            if (bytesRead > 0)
            {
                // Copy the read bytes to our memory buffer
                await memoryBuffer.WriteAsync(buffer, offset, bytesRead, cancellationToken);
            }
            
            return bytesRead;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();
        
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
