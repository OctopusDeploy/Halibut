#nullable enable
using System;
using System.IO;
using System.Runtime.Remoting;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Transport.Streams
{
    class HalibutLimitsStream : AsyncDisposableStream
    {
        readonly Stream inner;
        int readTimeout;
        int writeTimeout;

        public HalibutLimitsStream(Stream inner, TimeSpan readTimeout, TimeSpan writeTimeout)
        {
            this.inner = inner;
            this.readTimeout = readTimeout.Milliseconds;
            this.writeTimeout = writeTimeout.Milliseconds;
        }

        public override async ValueTask DisposeAsync()
        {
            await inner.DisposeAsync();
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

        public override void Close() => inner.Close();

        public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken) => await inner.CopyToAsync(destination, bufferSize, cancellationToken);
        
        public override int ReadByte() => inner.ReadByte();

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

        public override void Flush() => inner.Flush();

        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);

        public override void SetLength(long value) => inner.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);

        public override void WriteByte(byte value) => inner.WriteByte(value);

#if !NETFRAMEWORK
        public override void CopyTo(Stream destination, int bufferSize) => inner.CopyTo(destination, bufferSize);
        
        public override int Read(Span<byte> buffer) => inner.Read(buffer);

        public override void Write(ReadOnlySpan<byte> buffer) => inner.Write(buffer);
#endif

#if NETFRAMEWORK
        public override ObjRef CreateObjRef(Type requestedType) => inner.CreateObjRef(requestedType);

        public override object? InitializeLifetimeService() => inner.InitializeLifetimeService();
#endif

        public override int ReadTimeout
        {
            get => readTimeout;
            set => readTimeout = value;
        }

        public override int WriteTimeout
        {
            get => writeTimeout;
            set => writeTimeout = value;
        }

        public override bool CanTimeout => true;
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
