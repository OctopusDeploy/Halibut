using System;
using System.IO;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Transport.Streams;

namespace Halibut.Transport.Protocol
{
    public class WebSocketStream : AsyncDisposableStream
    {
        readonly WebSocket context;
        bool isDisposed;
        readonly CancellationTokenSource cancel = new();

        static readonly TimeSpan SendCancelTimeout = TimeSpan.FromSeconds(1);

        public WebSocketStream(WebSocket context)
        {
            this.context = context;
        }

        public override void Flush()
        {
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            AssertCanReadOrWrite();
            var segment = new ArraySegment<byte>(buffer, offset, count);
            var receiveResult = await context.ReceiveAsync(segment, cancellationToken);
            return receiveResult.Count;
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            AssertCanReadOrWrite();
            await context.SendAsync(new ArraySegment<byte>(buffer, offset, count), WebSocketMessageType.Binary, false, cancellationToken);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            AssertCanReadOrWrite();
            var segment = new ArraySegment<byte>(buffer, offset, count);
            var receiveResult = context.ReceiveAsync(segment, CancellationToken.None)
                .ConfigureAwait(false).GetAwaiter().GetResult();
            return receiveResult.Count;
        }

        public async Task<string?> ReadTextMessage(TimeSpan timeout, CancellationToken cancellationToken)
        {
            AssertCanReadOrWrite();
            var sb = new StringBuilder();
            var buffer = new ArraySegment<byte>(new byte[10000]);

            while (true)
            {
                var readResult = await CancellationAndTimeoutTaskWrapper.WrapWithCancellationAndTimeout(async ct =>
                    {
                        var result = await context.ReceiveAsync(buffer, ct).ConfigureAwait(false);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            using var sendCancel = new CancellationTokenSource(SendCancelTimeout);
                            await context.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Close received", sendCancel.Token).ConfigureAwait(false);
                            return new { Completed = true, Successful = false };
                        }

                        if (result.MessageType != WebSocketMessageType.Text)
                            throw new Exception($"Encountered an unexpected message type {result.MessageType}");

                        sb.Append(Encoding.UTF8.GetString(buffer.Array!, 0, result.Count));

                        return new { Completed = result.EndOfMessage, Successful = true };
                    },
                    onCancellationAction: null,
                    onActionTaskExceptionAction: null,
                    getExceptionOnTimeout: () =>
                    {
                        var socketException = new SocketException(10060);
                        return new IOException($"Unable to read data from the transport connection: {socketException.Message}.", socketException);
                    },
                    timeout,
                    nameof(ReadTextMessage),
                    cancellationToken);

                if (readResult.Completed)
                {
                    return readResult.Successful ? sb.ToString() : null;
                }
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            AssertCanReadOrWrite();
            context.SendAsync(new ArraySegment<byte>(buffer, offset, count), WebSocketMessageType.Binary, false, CancellationToken.None)
                .ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public async Task WriteTextMessage(string message, TimeSpan timeout, CancellationToken cancellationToken)
        {
            AssertCanReadOrWrite();
            var buffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(message));

            await CancellationAndTimeoutTaskWrapper.WrapWithCancellationAndTimeout(
                async ct =>
                {
                    await context.SendAsync(buffer, WebSocketMessageType.Text, true, ct);
                    return true;
                },
                onCancellationAction: null,
                onActionTaskExceptionAction: null,
                getExceptionOnTimeout: () =>
                {
                    var socketException = new SocketException(10060);
                    return new IOException($"Unable to write data to the transport connection: {socketException.Message}.", socketException);
                },
                timeout,
                nameof(WriteTextMessage),
                cancellationToken);
        }

        void AssertCanReadOrWrite()
        {
            if (isDisposed)
                throw new InvalidOperationException("Can not read or write a disposed stream");
            if (context.CloseStatus.HasValue)
                throw new Exception("Remote endpoint closed the stream");
        }

        public override bool CanRead => context.State == WebSocketState.Open;
        public override bool CanSeek => false;
        public override bool CanWrite => context.State == WebSocketState.Open;
        public override bool CanTimeout => true;
        public override int ReadTimeout { get; set; }
        public override int WriteTimeout { get; set; }

        public override long Length => throw new NotImplementedException();

        public override long Position
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        void SendCloseMessage()
        {
            if (context.State != WebSocketState.Open)
                return;

            using (var sendCancel = new CancellationTokenSource(TimeSpan.FromSeconds(1)))
                context.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Closing", sendCancel.Token)
                    .ConfigureAwait(false).GetAwaiter().GetResult();
        }

        async Task SendCloseMessageAsync()
        {
            if (context.State != WebSocketState.Open)
                return;

            using (var sendCancel = new CancellationTokenSource(TimeSpan.FromSeconds(1)))
            {
                await context.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Closing", sendCancel.Token).ConfigureAwait(false);
            }
        }

        public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            var buffer = new byte[bufferSize];
            var readLength = await ReadAsync(buffer, 0, bufferSize, cancellationToken);
            await destination.WriteAsync(buffer, 0, readLength, cancellationToken);
        }

        public override void Close()
        {
            context.Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    SendCloseMessage();
                }
                finally
                {
                    isDisposed = true;
                    cancel.Cancel();
                    context.Dispose();
                    cancel.Dispose();
                }
            }

            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            try
            {
                await SendCloseMessageAsync();
            }
            finally
            {
                isDisposed = true;
                cancel.Cancel();
                context.Dispose();
                cancel.Dispose();
            }
        }
    }
}
