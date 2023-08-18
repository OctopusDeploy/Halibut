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
        readonly Stream inner;

        public volatile int CloseCallCount;
        public volatile int DisposeBoolCallCount;
        public volatile int DisposeAsyncCallCount;
        public volatile int FlushAsyncCallCount;
        public volatile int ReadAsyncCallCount;
        public volatile int WriteAsyncCallCount;
        public volatile int ReadMemoryAsyncCallCount;
        public volatile int WriteMemoryAsyncCallCount;
        public volatile int CopyToAsyncCallCount;
        public volatile int ReadByteCallCount;
        public volatile int BeginReadCallCount;
        public volatile int EndReadCallCount;
        public volatile int BeginWriteCallCount;
        public volatile int EndWriteCallCount;
        public volatile int FlushCallCount;
        public volatile int ReadCallCount;
        public volatile int SeekCallCount;
        public volatile int SetLengthCallCount;
        public volatile int WriteCallCount;
        public volatile int WriteByteCallCount;
        public volatile int CopyToCallCount;
        public volatile int ReadSpanCallCount;
        public volatile int WriteSpanCallCount;
        public volatile int CreateObjRefCallCount;
        public volatile int InitializeLifetimeServiceCallCount;

        public CallCountingStream(Stream inner)
        {
            this.inner = inner;
        }

        public void Reset()
        {
            CloseCallCount = 0;
            DisposeBoolCallCount = 0;
            DisposeAsyncCallCount = 0;
            FlushAsyncCallCount = 0;
            ReadAsyncCallCount = 0;
            WriteAsyncCallCount = 0;
            ReadMemoryAsyncCallCount = 0;
            WriteMemoryAsyncCallCount = 0;
            CopyToAsyncCallCount = 0;
            ReadByteCallCount = 0;
            BeginReadCallCount = 0;
            EndReadCallCount = 0;
            BeginWriteCallCount = 0;
            EndWriteCallCount = 0;
            FlushCallCount = 0;
            ReadCallCount = 0;
            SeekCallCount = 0;
            SetLengthCallCount = 0;
            WriteCallCount = 0;
            WriteByteCallCount = 0;
            CopyToCallCount = 0;
            ReadSpanCallCount = 0;
            WriteSpanCallCount = 0;
            CreateObjRefCallCount = 0;
            InitializeLifetimeServiceCallCount = 0;
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
            Interlocked.Increment(ref FlushAsyncCallCount);
            await inner.FlushAsync(cancellationToken);
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref ReadAsyncCallCount);
            return await inner.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref WriteAsyncCallCount);
            await inner.WriteAsync(buffer, offset, count, cancellationToken);
        }

#if !NETFRAMEWORK
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref ReadMemoryAsyncCallCount);
            return await inner.ReadAsync(buffer, cancellationToken);
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref WriteMemoryAsyncCallCount);
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
            Interlocked.Increment(ref CopyToAsyncCallCount);
            await inner.CopyToAsync(destination, bufferSize, cancellationToken);
        }

        public override int ReadByte()
        {
            Interlocked.Increment(ref ReadByteCallCount);
            return inner.ReadByte();
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
        {
            Interlocked.Increment(ref BeginReadCallCount);
            return inner.BeginRead(buffer, offset, count, callback, state);
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            Interlocked.Increment(ref EndReadCallCount);
            return inner.EndRead(asyncResult);
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
        {
            Interlocked.Increment(ref BeginWriteCallCount);
            return inner.BeginWrite(buffer, offset, count, callback, state);
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            Interlocked.Increment(ref EndWriteCallCount);
            inner.EndWrite(asyncResult);
        }

        public override void Flush()
        {
            Interlocked.Increment(ref FlushCallCount);
            inner.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            Interlocked.Increment(ref ReadCallCount);
            return inner.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            Interlocked.Increment(ref SeekCallCount);
            return inner.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            Interlocked.Increment(ref SetLengthCallCount);
            inner.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            Interlocked.Increment(ref WriteCallCount);
            inner.Write(buffer, offset, count);
        }

        public override void WriteByte(byte value)
        {
            Interlocked.Increment(ref WriteByteCallCount);
            inner.WriteByte(value);
        }

#if !NETFRAMEWORK
        public override void CopyTo(Stream destination, int bufferSize)
        {
            Interlocked.Increment(ref CopyToCallCount);
            inner.CopyTo(destination, bufferSize);
        }

        public override int Read(Span<byte> buffer)
        {
            Interlocked.Increment(ref ReadSpanCallCount);
            return inner.Read(buffer);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            Interlocked.Increment(ref WriteSpanCallCount);
            inner.Write(buffer);
        }
#endif

#if NETFRAMEWORK
        public override ObjRef CreateObjRef(Type requestedType)
        {
            Interlocked.Increment(ref CreateObjRefCallCount);
            return inner.CreateObjRef(requestedType);
        }

        public override object? InitializeLifetimeService()
        {
            Interlocked.Increment(ref InitializeLifetimeServiceCallCount);
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
