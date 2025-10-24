using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Transport.Streams;

namespace Halibut.Tests.Support.Streams
{
    public class ActionBeforeWriteAndCountingStream : AsyncStream
    {
        readonly Stream stream;
        long writtenSoFar;
        public Action<long> BeforeWrite;

        public ActionBeforeWriteAndCountingStream(Stream stream, Action<long> beforeWrite)
        {
            this.stream = stream;
            BeforeWrite = beforeWrite;
        }

        public override void Flush()
        {
            stream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return stream.Read(buffer, offset, count);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return stream.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            BeforeWrite(writtenSoFar);
            await stream.WriteAsync(buffer, offset, count, cancellationToken);
            writtenSoFar += count;
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return stream.FlushAsync(cancellationToken);
        }

        public override ValueTask DisposeAsync()
        {
            return stream.DisposeAsync();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return stream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            stream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            BeforeWrite(writtenSoFar);
            stream.Write(buffer, offset, count);
            writtenSoFar += count;
        }

        public override bool CanRead => stream.CanRead;

        public override bool CanSeek => stream.CanSeek;

        public override bool CanWrite => stream.CanWrite;

        public override long Length => stream.Length;

        public override long Position
        {
            get => stream.Position;
            set => stream.Position = value;
        }
    }
}