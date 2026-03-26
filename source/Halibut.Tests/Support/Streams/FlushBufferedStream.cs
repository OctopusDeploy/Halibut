#nullable enable
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Tests.Support.Streams
{
    /// <summary>
    /// A stream that buffers all writes in memory and only forwards them to the underlying
    /// stream when Flush or FlushAsync is called.
    /// </summary>
    public class TestOnlySendDataWhenFlushedStream : DelegateStreamBase
    {
        readonly Stream inner;
        MemoryStream writeBuffer = new MemoryStream();

        public TestOnlySendDataWhenFlushedStream(Stream inner)
        {
            this.inner = inner;
        }

        public override Stream Inner => inner;

        public override void Write(byte[] buffer, int offset, int count)
            => writeBuffer.Write(buffer, offset, count);

        public override void WriteByte(byte value)
            => writeBuffer.WriteByte(value);

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => writeBuffer.WriteAsync(buffer, offset, count, cancellationToken);

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
            => writeBuffer.BeginWrite(buffer, offset, count, callback, state);

        public override void EndWrite(IAsyncResult asyncResult)
            => writeBuffer.EndWrite(asyncResult);

#if !NETFRAMEWORK
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            => await writeBuffer.WriteAsync(buffer, cancellationToken);

        public override void Write(ReadOnlySpan<byte> buffer)
            => writeBuffer.Write(buffer);
#endif

        public override void Flush()
        {
            writeBuffer.Position = 0;
            writeBuffer.CopyTo(inner);
            writeBuffer = new MemoryStream();
            inner.Flush();
        }

        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            writeBuffer.Position = 0;
            await writeBuffer.CopyToAsync(inner, 8192, cancellationToken);
            writeBuffer = new MemoryStream();
            await inner.FlushAsync(cancellationToken);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                writeBuffer.Dispose();
            }
            base.Dispose(disposing);
        }

#if !NETFRAMEWORK
        public override async ValueTask DisposeAsync()
        {
            await writeBuffer.DisposeAsync();
            await inner.DisposeAsync();
        }
#endif
    }
}
