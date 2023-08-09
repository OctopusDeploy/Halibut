using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Transport
{
    /// <summary>
    /// Each read will not read more than the specified number of bytes.
    ///
    /// Useful when testing that code actually deals with a read that doesn't return everything in
    /// one read operation.
    /// </summary>
    public class LimitedNumberOfBytesPerReadStream : Stream
    {
        readonly Stream baseStream;
        readonly int maxNumberOfBytesToReadAtATime;

        public LimitedNumberOfBytesPerReadStream(Stream baseStream, int maxNumberOfBytesToReadAtATime)
        {
            this.baseStream = baseStream;
            this.maxNumberOfBytesToReadAtATime = maxNumberOfBytesToReadAtATime;
        }

        public override void Flush() => baseStream.Flush();
        
        public override int Read(byte[] buffer, int offset, int count)
        {
            count = ReduceReadCountToBufferSize(count);
            return baseStream.Read(buffer, offset, count);
        }
        
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            count = ReduceReadCountToBufferSize(count);
            return await baseStream.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public override long Seek(long offset, SeekOrigin origin) => baseStream.Seek(offset, origin);

        public override void SetLength(long value) => baseStream.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count)
        {
            baseStream.Write(buffer, offset, count);
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await baseStream.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override bool CanTimeout => baseStream.CanTimeout;
        
        public override int ReadTimeout
        {
            get => baseStream.ReadTimeout;
            set => baseStream.ReadTimeout = value;
        }

        public override int WriteTimeout
        {
            get => baseStream.WriteTimeout;
            set => baseStream.WriteTimeout = value;
        }

        public override long Length => baseStream.Length;

        public override long Position
        {
            get => baseStream.Position;
            set => baseStream.Position = value;
        }
        

        private int ReduceReadCountToBufferSize(int count)
        {
            return Math.Min(maxNumberOfBytesToReadAtATime, count);
        }
    }
}
