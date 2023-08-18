#nullable enable
using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Transport.Streams;
#if NETFRAMEWORK
using System.Runtime.Remoting;
#endif

namespace Halibut.Tests.Transport.Streams
{
    class PausingStream : AsyncDisposableStream
    {
        readonly Stream inner;
        bool paused;
        CancellationTokenSource syncReadPauseCancellationTokenSource;
        CancellationTokenSource syncWritePauseCancellationTokenSource;
        CancellationToken asyncCancellationToken;

        bool pauseDisposeOrClose;
        int readTimeout;
        int writeTimeout;

        public PausingStream(Stream inner)
        {
            this.inner = inner;
            this.readTimeout = inner.ReadTimeout;
            this.writeTimeout = inner.WriteTimeout;
        }

        public void PauseUntilTimeout(CancellationToken asyncCancellationToken, bool pauseDisposeOrClose = true)
        {
            this.asyncCancellationToken = asyncCancellationToken;
            syncReadPauseCancellationTokenSource = new CancellationTokenSource(this.readTimeout);
            syncWritePauseCancellationTokenSource = new CancellationTokenSource(this.writeTimeout);
            this.pauseDisposeOrClose = pauseDisposeOrClose;
            paused = true;
        }
        
        async Task PauseForeverIfPaused()
        {
            if (paused)
            {
                await Task.Delay(-1, asyncCancellationToken);
            }
        }

        void PauseUntilTimeoutIfPausedSync(bool read, CancellationToken? cancellationToken)
        {
            if (paused)
            {
                if (cancellationToken == null)
                {
                    throw new NotSupportedException("Cancellation token must be provided or this will sleep forever");
                }

                while (!cancellationToken!.Value.IsCancellationRequested)
                {
                    Thread.Sleep(10);
                }

                // For sync we need to mimic a socket timing out as we don't we rely on the correct timeout behaviour for sync
                // Simulating this exception
                // actualException.GetType().Name
                // "IOException"
                // actualException.Message
                // "Unable to read data from the transport connection: A connection attempt failed because the connected party did not properly respond after a period of time, or established connection failed because connected host has failed to respond.."
                // actualException.InnerException.GetType().Name
                // "SocketException"
                // (actualException.InnerException).Message
                // "A connection attempt failed because the connected party did not properly respond after a period of time, or established connection failed because connected host has failed to respond."
                // ((SocketException)actualException.InnerException).ErrorCode
                // 10060 
                throw new IOException(
                    $"Unable to {(read ? "read" : "write")} data {(read ? "from" : "to")} the transport connection: A connection attempt failed because the connected party did not properly respond after a period of time, or established connection failed because connected host has failed to respond.",
                    new SocketException((int)SocketError.TimedOut));
            }
        }

        public override async ValueTask DisposeAsync()
        {
            if(pauseDisposeOrClose)
                await PauseForeverIfPaused();

            await inner.DisposeAsync();
        }

        protected override void Dispose(bool disposing)
        {
            if(pauseDisposeOrClose)
                PauseUntilTimeoutIfPausedSync(false, syncWritePauseCancellationTokenSource?.Token);
        }

        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            await PauseForeverIfPaused();
            await inner.FlushAsync(cancellationToken);
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await PauseForeverIfPaused();
            return await inner.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await PauseForeverIfPaused();
            await inner.WriteAsync(buffer, offset, count, cancellationToken);
        }

#if !NETFRAMEWORK
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await PauseForeverIfPaused();
            return await inner.ReadAsync(buffer, cancellationToken);
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await PauseForeverIfPaused();
            await inner.WriteAsync(buffer, cancellationToken);
        }
#endif

        public override void Close()
        {
            if(pauseDisposeOrClose)
                PauseUntilTimeoutIfPausedSync(false, syncWritePauseCancellationTokenSource?.Token);

            inner.Close();
        }

        public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            await PauseForeverIfPaused();
            await inner.CopyToAsync(destination, bufferSize, cancellationToken);
        }

        public override int ReadByte()
        {
            PauseUntilTimeoutIfPausedSync(true, syncReadPauseCancellationTokenSource?.Token);
            return inner.ReadByte();
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
        {
            PauseUntilTimeoutIfPausedSync(true, syncReadPauseCancellationTokenSource?.Token);
            return inner.BeginRead(buffer, offset, count, callback, state);
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            PauseUntilTimeoutIfPausedSync(true, syncReadPauseCancellationTokenSource?.Token);
            return inner.EndRead(asyncResult);
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
        {
            PauseUntilTimeoutIfPausedSync(false, syncWritePauseCancellationTokenSource?.Token);
            return inner.BeginWrite(buffer, offset, count, callback, state);
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            PauseUntilTimeoutIfPausedSync(false, syncWritePauseCancellationTokenSource?.Token);
            inner.EndWrite(asyncResult);
        }

        public override void Flush()
        {
            PauseUntilTimeoutIfPausedSync(false, syncWritePauseCancellationTokenSource?.Token);
            inner.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            PauseUntilTimeoutIfPausedSync(true, syncReadPauseCancellationTokenSource?.Token);
            return inner.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            PauseUntilTimeoutIfPausedSync(false, syncWritePauseCancellationTokenSource?.Token);
            return inner.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            PauseUntilTimeoutIfPausedSync(false, syncWritePauseCancellationTokenSource?.Token);
            inner.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            PauseUntilTimeoutIfPausedSync(false, syncWritePauseCancellationTokenSource?.Token);
            inner.Write(buffer, offset, count);
        }

        public override void WriteByte(byte value)
        {
            PauseUntilTimeoutIfPausedSync(false, syncWritePauseCancellationTokenSource?.Token);
            inner.WriteByte(value);
        }

#if !NETFRAMEWORK
        public override void CopyTo(Stream destination, int bufferSize)
        {
            PauseUntilTimeoutIfPausedSync(false, syncWritePauseCancellationTokenSource?.Token);
            inner.CopyTo(destination, bufferSize);
        }

        public override int Read(Span<byte> buffer)
        {
            PauseUntilTimeoutIfPausedSync(true, syncReadPauseCancellationTokenSource?.Token);
            return inner.Read(buffer);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            PauseUntilTimeoutIfPausedSync(false, syncWritePauseCancellationTokenSource?.Token);
            inner.Write(buffer);
        }
#endif

#if NETFRAMEWORK
        public override ObjRef CreateObjRef(Type requestedType)
        {
            PauseUntilTimeoutIfPausedSync(false, syncWritePauseCancellationTokenSource?.Token);
            return inner.CreateObjRef(requestedType);
        }

        public override object? InitializeLifetimeService()
        {
            PauseUntilTimeoutIfPausedSync(false, syncWritePauseCancellationTokenSource?.Token);
            return inner.InitializeLifetimeService();
        }
#endif

        public override int ReadTimeout
        {
            get => inner.ReadTimeout;
            set
            {
                readTimeout = value;
                inner.ReadTimeout = value;
            }
        }

        public override int WriteTimeout
        {
            get => inner.WriteTimeout;
            set
            {
                writeTimeout = value;
                inner.WriteTimeout = value;
            }
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
