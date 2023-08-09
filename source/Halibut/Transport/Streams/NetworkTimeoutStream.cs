#nullable enable
using System;
using System.IO;
using System.Net.Sockets;
using System.Runtime.Remoting;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Util;

namespace Halibut.Transport.Streams
{
    class NetworkTimeoutStream : Stream
    {
        readonly Stream inner;

        public NetworkTimeoutStream(Stream inner)
        {
            this.inner = inner;
        }

        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            await WrapWithCancellationAndTimeout(
                async ct =>
                {
                    await inner.FlushAsync(ct);
                    return 0;
                },
                CanTimeout ? WriteTimeout : int.MaxValue,
                false,
                nameof(FlushAsync),
                cancellationToken);
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return await WrapWithCancellationAndTimeout(
                async ct => await inner.ReadAsync(buffer, offset, count, ct),
                CanTimeout ? ReadTimeout : int.MaxValue,
                true,
                nameof(ReadAsync),
                cancellationToken);
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await WrapWithCancellationAndTimeout(
                async ct =>
                {
                    await inner.WriteAsync(buffer, offset, count, ct);
                    return 0;
                },
                CanTimeout ? WriteTimeout : int.MaxValue,
                false,
                nameof(WriteAsync),
                cancellationToken);
        }

#if !NETFRAMEWORK
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return await WrapWithCancellationAndTimeout(
                async ct => await inner.ReadAsync(buffer, ct),
                CanTimeout ? ReadTimeout : int.MaxValue,
                true,
                nameof(ReadAsync),
                cancellationToken);
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await WrapWithCancellationAndTimeout(
                async ct =>
                {
                    await inner.WriteAsync(buffer, ct);
                    return 0;
                },
                CanTimeout ? WriteTimeout : int.MaxValue,
                false,
                nameof(WriteAsync),
                cancellationToken);
        }
#endif

        async Task<T> WrapWithCancellationAndTimeout<T>(
            Func<CancellationToken, Task<T>> action,
            bool isRead,
            CancellationToken cancellationToken)
        {
            using var timeoutCancellationTokenSource = new CancellationTokenSource(timeout);
            using var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCancellationTokenSource.Token);

            var actionTask = action(linkedCancellationTokenSource.Token);
            var cancellationTask = linkedCancellationTokenSource.Token.AsTask<T>();

            try
            {
                var completedTask = await Task.WhenAny(actionTask, cancellationTask);

                if (completedTask == cancellationTask)
                {
                    actionTask.IgnoreUnobservedExceptions();

                    try
                    {
                        inner.Close();
                    }
                    catch { }

                    ThrowMeaningfulException();
                }

                return await actionTask;
            }
            catch (Exception e)
            {
                ThrowMeaningfulException(e);

                throw;
            }

            void ThrowMeaningfulException(Exception? innerException = null)
            {
                if (timeoutCancellationTokenSource.IsCancellationRequested)
                {
                    var socketException = new SocketException(10060);
                    throw new IOException($"Unable to {(isRead ? "read data from" : "write data to")} the transport connection: {socketException.Message}.", socketException);
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException($"The {methodName} operation was cancelled.", innerException);
                }
            }
        }

        public override void Close() => inner.Close();

        public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken) => await inner.CopyToAsync(destination, bufferSize, cancellationToken);

        public override int EndRead(IAsyncResult asyncResult) => inner.EndRead(asyncResult);

        public override void EndWrite(IAsyncResult asyncResult) => inner.EndWrite(asyncResult);

        public override int ReadByte() => inner.ReadByte();

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state) => inner.BeginRead(buffer, offset, count, callback, state);

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state) => inner.BeginWrite(buffer, offset, count, callback, state);

        public override void Flush() => inner.Flush();

        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);

        public override void SetLength(long value) => inner.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);

        public override void WriteByte(byte value) => inner.WriteByte(value);

#if !NETFRAMEWORK
        public override void CopyTo(Stream destination, int bufferSize) => inner.CopyTo(destination, bufferSize);
        
        public override async ValueTask DisposeAsync() => await inner.DisposeAsync();
        
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

        public bool DataAvailable
        {
            get
            {
                if (inner is NetworkStream networkStream)
                {
                    return networkStream.DataAvailable;
                }

                throw new NotSupportedException($"{nameof(DataAvailable)} is only available when wrapping a {nameof(NetworkStream)}");
            }
        }
    }
}
