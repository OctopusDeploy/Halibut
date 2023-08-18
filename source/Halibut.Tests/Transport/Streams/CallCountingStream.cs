#nullable enable
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Transport.Streams;
#if NETFRAMEWORK
using System.Runtime.Remoting;
#endif

namespace Halibut.Tests.Transport.Streams
{
    class CallCountingStream : AsyncDisposableStream
    {
        public volatile int CloseCallCount = 0;

        public volatile int DisposeBoolCallCount = 0;
        public volatile int DisposeAsyncCallCount = 0;

        readonly Stream inner;

        public CallCountingStream(Stream inner)
        {
            this.inner = inner;
        }

        public override async ValueTask DisposeAsync()
        {
            Interlocked.Increment(ref DisposeAsyncCallCount);

            await inner.DisposeAsync();
        }

        protected override void Dispose(bool disposing)
        {
            Interlocked.Increment(ref DisposeBoolCallCount);
        }

        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            await inner.FlushAsync(cancellationToken);
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return await inner.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await inner.WriteAsync(buffer, offset, count, cancellationToken);
        }

#if !NETFRAMEWORK
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return await inner.ReadAsync(buffer, cancellationToken);
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await inner.WriteAsync(buffer, cancellationToken);
        }
#endif

        public override void Close()
        {
            Interlocked.Increment(ref CloseCallCount);

            inner.Close();
        }

        public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            await inner.CopyToAsync(destination, bufferSize, cancellationToken);
        }

        public override int ReadByte()
        {
            return inner.ReadByte();
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
        {
            return inner.BeginRead(buffer, offset, count, callback, state);
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            return inner.EndRead(asyncResult);
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
        {
            return inner.BeginWrite(buffer, offset, count, callback, state);
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            inner.EndWrite(asyncResult);
        }

        public override void Flush()
        {
            inner.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return inner.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return inner.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            inner.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            inner.Write(buffer, offset, count);
        }

        public override void WriteByte(byte value)
        {
            inner.WriteByte(value);
        }

#if !NETFRAMEWORK
        public override void CopyTo(Stream destination, int bufferSize)
        {
            inner.CopyTo(destination, bufferSize);
        }

        public override int Read(Span<byte> buffer)
        {
            return inner.Read(buffer);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            inner.Write(buffer);
        }
#endif

#if NETFRAMEWORK
        public override ObjRef CreateObjRef(Type requestedType)
        {
            return inner.CreateObjRef(requestedType);
        }

        public override object? InitializeLifetimeService()
        {
            return inner.InitializeLifetimeService();
        }
#endif

        public override int ReadTimeout
        {
            get => inner.ReadTimeout;
            set => inner.ReadTimeout = value;
        }

        public override int WriteTimeout
        {
            get => inner.WriteTimeout;
            set => inner.WriteTimeout = value;
        }

        public override bool CanTimeout => inner.CanTimeout;
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => inner.CanWrite;
        public override long Length => inner.Length;
        public override long Position
        {
            get => inner.Position;
            set => inner.Position = value;
        }
    }
}
