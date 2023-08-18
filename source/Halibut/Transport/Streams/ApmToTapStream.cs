#nullable enable
using System;
using System.IO;
using System.Runtime.Remoting;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Util;

namespace Halibut.Transport.Streams
{
    /// <summary>
    /// Ensures that calls to old APM-style async methods are redirected
    /// to new TAP-style async methods.
    /// </summary>
    class ApmToTapStream : AsyncDisposableStream
    {
        readonly Stream inner;

        public ApmToTapStream(Stream inner)
        {
            this.inner = inner;
        }

        public override async ValueTask DisposeAsync() => await inner.DisposeAsync();

        public override async Task FlushAsync(CancellationToken cancellationToken) => await inner.FlushAsync(cancellationToken);

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => await inner.ReadAsync(buffer, offset, count, cancellationToken);

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => await inner.WriteAsync(buffer, offset, count, cancellationToken);

#if !NETFRAMEWORK
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => await inner.ReadAsync(buffer, cancellationToken);

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => await inner.WriteAsync(buffer, cancellationToken);
#endif

        public override void Close() => inner.Close();

        public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken) => await inner.CopyToAsync(destination, bufferSize, cancellationToken);
        
        public override int ReadByte() => inner.ReadByte();

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
        {
            // Don't continue down the `BeginRead` execution path, as that will eventually go sync.
            // Redirect to ReadAsync to ensure code execution stays async.
            return ReadAsync(buffer, offset, count, CancellationToken.None).AsAsynchronousProgrammingModel(callback, state);
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            try
            {
                return ((Task<int>)asyncResult).Result;
            }
            catch (AggregateException e) when (e.InnerExceptions.Count == 1 && e.InnerException is not null)
            {
                throw e.InnerException;
            }
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
        {
            // Don't continue down the `BeginWrite` execution path, as that will eventually go sync.
            // Redirect to WriteAsync to ensure code execution stays async.
            return WriteAsync(buffer, offset, count, CancellationToken.None).AsAsynchronousProgrammingModel(callback, state);
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            var task = (Task)asyncResult;
            try
            {
                Task.WaitAll(task);
            }
            catch (AggregateException e) when (e.InnerExceptions.Count == 1 && e.InnerException is not null)
            {
                throw e.InnerException;
            }
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
