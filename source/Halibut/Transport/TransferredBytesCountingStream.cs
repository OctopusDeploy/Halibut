using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Transport
{
    class TransferredBytesCountingStream : Stream
    {
        readonly Stream baseStream;
        public long TotalWritten = 0;
        public long TotalRead = 0;

        public TransferredBytesCountingStream(Stream baseStream)
        {
            this.baseStream = baseStream;
        }

        public long TotalTransferred() => TotalWritten + TotalRead;

        public override void Flush() => baseStream.Flush();
        
        public override int Read(byte[] buffer, int offset, int count)
        {
            var baseCount = baseStream.Read(buffer, offset, count);
            TotalRead += baseCount;
            return baseCount;
        }

        public async override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var baseCount = await baseStream.ReadAsync(buffer, offset, count, cancellationToken);
            TotalRead += baseCount;
            return baseCount;
        }

        public override long Seek(long offset, SeekOrigin origin) => baseStream.Seek(offset, origin);

        public override void SetLength(long value) => baseStream.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count)
        {
            baseStream.Write(buffer, offset, count);
            TotalWritten += count;
        }

        public async override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await baseStream.WriteAsync(buffer, offset, count, cancellationToken);
            TotalWritten += count;
        }

        public override bool CanRead => baseStream.CanRead;
        public override bool CanSeek => baseStream.CanSeek;
        public override bool CanWrite => baseStream.CanWrite;

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
    }
}
