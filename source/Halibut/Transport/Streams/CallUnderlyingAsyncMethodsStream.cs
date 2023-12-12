using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System.Runtime.Remoting;
using System;

namespace Halibut.Transport.Streams
{
    class CallUnderlyingAsyncMethodsStream : AsyncStream
    {
        readonly Stream inner;

        public CallUnderlyingAsyncMethodsStream(Stream inner)
        {
            this.inner = inner;
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

        public override void Close()
        {
            inner.Close();
        }

        public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            await base.CopyToAsync(destination, bufferSize, cancellationToken);
        }

        public override int ReadByte()
        {
            return inner.ReadByte();
        }

        public override void Flush()
        {
            inner.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return inner.ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();
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
            inner.WriteAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();
        }

        public override void WriteByte(byte value)
        {
            inner.WriteByte(value);
        }

#if !NETFRAMEWORK
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