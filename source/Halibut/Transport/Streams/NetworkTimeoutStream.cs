#nullable enable
using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Util;

#if NETFRAMEWORK
using System.Runtime.Remoting;
#endif

namespace Halibut.Transport.Streams
{
    class NetworkTimeoutStream : AsyncStream
    {
        readonly Stream inner;
        bool hasCancelledOrTimedOut = false;
        Exception? cancellationOrTimeoutException = null;

        public NetworkTimeoutStream(Stream inner)
        {
            this.inner = inner;
        }

        public override async ValueTask DisposeAsync()
        {
            await inner.DisposeAsync();
        }

        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            ThrowIfAlreadyCancelledOrTimedOut();

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
            ThrowIfAlreadyCancelledOrTimedOut();

            return await WrapWithCancellationAndTimeout(
                async ct => await inner.ReadAsync(buffer, offset, count, ct),
                CanTimeout ? ReadTimeout : int.MaxValue,
                true,
                nameof(ReadAsync),
                cancellationToken);
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            ThrowIfAlreadyCancelledOrTimedOut();

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
            ThrowIfAlreadyCancelledOrTimedOut();

            return await WrapWithCancellationAndTimeout(
                async ct => await inner.ReadAsync(buffer, ct),
                CanTimeout ? ReadTimeout : int.MaxValue,
                true,
                nameof(ReadAsync),
                cancellationToken);
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ThrowIfAlreadyCancelledOrTimedOut();

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

        public override void Close()
        {
            inner.Close();
        }

        public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            ThrowIfAlreadyCancelledOrTimedOut();

            await base.CopyToAsync(destination, bufferSize, cancellationToken);
        }

        public override int ReadByte()
        {
            return TryCloseOnTimeout(() => inner.ReadByte());
        }

        public override void Flush()
        {
            TryCloseOnTimeout(() => inner.Flush());
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return TryCloseOnTimeout(() => inner.Read(buffer, offset, count));
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            ThrowIfAlreadyCancelledOrTimedOut();

            return inner.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            ThrowIfAlreadyCancelledOrTimedOut();

            inner.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            TryCloseOnTimeout(() => inner.Write(buffer, offset, count));
        }

        public override void WriteByte(byte value)
        {
            TryCloseOnTimeout(() => inner.WriteByte(value));
        }

#if !NETFRAMEWORK
        public override void CopyTo(Stream destination, int bufferSize)
        {
            ThrowIfAlreadyCancelledOrTimedOut();

            base.CopyTo(destination, bufferSize);
        }

        public override int Read(Span<byte> buffer)
        {
            ThrowIfAlreadyCancelledOrTimedOut();

            try
            {
                return inner.Read(buffer);
            }
            catch (Exception ex) when (IsTimeoutException(ex))
            {
                try
                {
                    CloseOnTimeout(ex);
                }
                catch { }

                throw;
            }
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            ThrowIfAlreadyCancelledOrTimedOut();

            try
            {
                inner.Write(buffer);
            }
            catch (Exception ex) when (IsTimeoutException(ex))
            {
                try
                {
                    CloseOnTimeout(ex);
                }
                catch { }

                throw;
            }
        }
#endif

#if NETFRAMEWORK
        public override ObjRef CreateObjRef(Type requestedType)
        {
            ThrowIfAlreadyCancelledOrTimedOut();
            
            return inner.CreateObjRef(requestedType);
        }

        public override object? InitializeLifetimeService()
        {
            ThrowIfAlreadyCancelledOrTimedOut();

            return inner.InitializeLifetimeService();
        }
#endif

        public override int ReadTimeout
        {
            get
            {
                ThrowIfAlreadyCancelledOrTimedOut();
                return inner.ReadTimeout;
            }
            set
            {
                ThrowIfAlreadyCancelledOrTimedOut();
                inner.ReadTimeout = value;
            }
        }

        public override int WriteTimeout
        {
            get
            {
                ThrowIfAlreadyCancelledOrTimedOut();
                return inner.WriteTimeout;
            }
            set
            {
                ThrowIfAlreadyCancelledOrTimedOut();
                inner.WriteTimeout = value;
            }
        }

        public override bool CanTimeout
        {
            get
            {
                ThrowIfAlreadyCancelledOrTimedOut();
                return inner.CanTimeout;
            }
        }

        public override bool CanRead
        {
            get
            {
                ThrowIfAlreadyCancelledOrTimedOut();
                return inner.CanRead;
            }
        }

        public override bool CanSeek
        {
            get
            {
                ThrowIfAlreadyCancelledOrTimedOut();
                return inner.CanSeek;
            }
        }

        public override bool CanWrite
        {
            get
            {
                ThrowIfAlreadyCancelledOrTimedOut();
                return inner.CanWrite;
            }
        }

        public override long Length
        {
            get
            {
                ThrowIfAlreadyCancelledOrTimedOut();
                return inner.Length;
            }
        }

        public override long Position
        {
            get
            {
                ThrowIfAlreadyCancelledOrTimedOut();
                return inner.Position;
            }
            set
            {
                ThrowIfAlreadyCancelledOrTimedOut();
                inner.Position = value;
            }
        }

        async Task<T> WrapWithCancellationAndTimeout<T>(
            Func<CancellationToken, Task<T>> action,
            int timeout,
            bool isRead,
            string methodName,
            CancellationToken cancellationToken)
        {
            return await CancellationAndTimeoutTaskWrapper.WrapWithCancellationAndTimeout(
                action,
                onCancellationAction: async cancellationException => await SafelyDisposeStream(cancellationException),
                onActionTaskExceptionAction: async (exception, operationTimedOut) =>
                {
                    if (IsTimeoutException(exception) || operationTimedOut)
                    {
                        await SafelyDisposeStream(CreateExceptionOnTimeout());
                    }
                },
                CreateExceptionOnTimeout,
                TimeSpan.FromMilliseconds(timeout),
                methodName,
                cancellationToken);

            async Task SafelyDisposeStream(Exception exception)
            {
                cancellationOrTimeoutException = exception;
                hasCancelledOrTimedOut = true;

                await Try.CatchingError(async () => await DisposeAsync(), _ => { });
            }

            Exception CreateExceptionOnTimeout()
            {
                var socketException = new SocketException(10060);
                return new IOException($"Unable to {(isRead ? "read data from" : "write data to")} the transport connection: {socketException.Message}.", socketException);
            }
        }

        void TryCloseOnTimeout(Action action)
        {
            ThrowIfAlreadyCancelledOrTimedOut();

            try
            {
                action();
            }
            catch (Exception ex) when (IsTimeoutException(ex))
            {
                try
                {
                    CloseOnTimeout(ex);
                }
                catch { }

                throw;
            }
        }

        T TryCloseOnTimeout<T>(Func<T> action)
        {
            ThrowIfAlreadyCancelledOrTimedOut();

            try
            {
                return action();
            }
            catch (Exception ex) when (IsTimeoutException(ex))
            {
                try
                {
                    CloseOnTimeout(ex);
                }
                catch { }

                throw;
            }
        }

        void CloseOnTimeout(Exception ex)
        {
            cancellationOrTimeoutException = ex;
            hasCancelledOrTimedOut = true;
            inner.Close();
        }

        static bool IsTimeoutException(Exception exception)
        {
            if (exception is SocketException { SocketErrorCode: SocketError.TimedOut })
            {
                return true;
            }

            return exception.InnerException != null && IsTimeoutException(exception.InnerException);
        }

        void ThrowIfAlreadyCancelledOrTimedOut()
        {
            if (hasCancelledOrTimedOut)
            {
                throw cancellationOrTimeoutException ?? new SocketException((int)SocketError.TimedOut);
            }
        }
    }
}