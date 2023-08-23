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
        bool hasTimedOut = false;
        Exception? timeoutException = null;

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

        public override void Close()
        {
            inner.Close();
        }

        public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            ThrowIfAlreadyTimedOut();
            
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
            ThrowIfAlreadyTimedOut();
            
            return inner.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            ThrowIfAlreadyTimedOut();
            
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
            ThrowIfAlreadyTimedOut();
            
            base.CopyTo(destination, bufferSize);
        }
        
        public override int Read(Span<byte> buffer)
        {
            ThrowIfAlreadyTimedOut();

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
            ThrowIfAlreadyTimedOut();

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
            ThrowIfAlreadyTimedOut();
            
            return inner.CreateObjRef(requestedType);
        }

        public override object? InitializeLifetimeService()
        {
            ThrowIfAlreadyTimedOut();

            return inner.InitializeLifetimeService();
        }
#endif

        public override int ReadTimeout
        {
            get
            {
                ThrowIfAlreadyTimedOut();
                return inner.ReadTimeout;
            }
            set
            {
                ThrowIfAlreadyTimedOut();
                inner.ReadTimeout = value;
            }
        }

        public override int WriteTimeout
        {
            get
            {
                ThrowIfAlreadyTimedOut();
                return inner.WriteTimeout;
            }
            set
            {
                ThrowIfAlreadyTimedOut();
                inner.WriteTimeout = value;
            }
        }

        public override bool CanTimeout
        {
            get
            {
                ThrowIfAlreadyTimedOut();
                return inner.CanTimeout;
            }
        }

        public override bool CanRead
        {
            get
            {
                ThrowIfAlreadyTimedOut();
                return inner.CanRead;
            }
        }

        public override bool CanSeek
        {
            get
            {
                ThrowIfAlreadyTimedOut();
                return inner.CanSeek;
            }
        }

        public override bool CanWrite
        {
            get
            {
                ThrowIfAlreadyTimedOut();
                return inner.CanWrite;
            }
        }

        public override long Length
        {
            get
            {
                ThrowIfAlreadyTimedOut();
                return inner.Length;
            }
        }

        public override long Position
        {
            get
            {
                ThrowIfAlreadyTimedOut();
                return inner.Position;
            }
            set
            {
                ThrowIfAlreadyTimedOut();
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
            ThrowIfAlreadyTimedOut();

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
                try
                {
                    timeoutException = exception;
                    hasTimedOut = true;
                    await inner.DisposeAsync();
                }
                catch
                {
                }
            }

            Exception CreateExceptionOnTimeout()
            {
                var socketException = new SocketException(10060);
                return new IOException($"Unable to {(isRead ? "read data from" : "write data to")} the transport connection: {socketException.Message}.", socketException);
            }
        }

        void TryCloseOnTimeout(Action action)
        {
            ThrowIfAlreadyTimedOut();

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
            ThrowIfAlreadyTimedOut();

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
            timeoutException = ex;
            hasTimedOut = true;
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

        void ThrowIfAlreadyTimedOut()
        {
            if (hasTimedOut)
            {
                throw timeoutException ?? new SocketException((int)SocketError.TimedOut);
            }
        }
    }
}