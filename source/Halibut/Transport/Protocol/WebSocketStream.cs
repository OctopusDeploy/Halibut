using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Util;
using Janitor;

namespace Halibut.Transport.Protocol
{
    [SkipWeaving]
    public class WebSocketStream : Stream
    {
        readonly WebSocket context;
        bool isDisposed;
        readonly CancellationTokenSource cancel = new CancellationTokenSource();

        public WebSocketStream(WebSocket context)
        {
            this.context = context;
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return TaskEx.CompletedTask;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            AssertCanReadOrWrite();
            var segment = new ArraySegment<byte>(buffer, offset, count);
            var recieveResult = await context.ReceiveAsync(segment, cancellationToken).ConfigureAwait(false);
            return recieveResult.Count;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public async Task<string> ReadTextMessage()
        {
            AssertCanReadOrWrite();
            var sb = new StringBuilder();
            var buffer = new ArraySegment<byte>(new byte[10000]);
            while (true)
            {
                var result = await context.ReceiveAsync(buffer, cancel.Token).ConfigureAwait(false);
                if (result.MessageType != WebSocketMessageType.Text)
                    throw new Exception($"Encountered an unexpected message type {result.MessageType}");
                sb.Append(Encoding.UTF8.GetString(buffer.Array, 0, result.Count));

                if (result.EndOfMessage)
                    return sb.ToString();
            }
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            AssertCanReadOrWrite();
            return context.SendAsync(new ArraySegment<byte>(buffer, offset, count), WebSocketMessageType.Binary, false, cancellationToken);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public Task WriteTextMessage(string message)
        {
            AssertCanReadOrWrite();
            var buffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(message));
            return context.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
        }

        void AssertCanReadOrWrite()
        {
            if (isDisposed)
                throw new InvalidOperationException("Can not read or write a disposed stream");
            if (context.CloseStatus.HasValue)
                throw new Exception("Remote endpoint closed the stream");
        }

        public override bool CanRead => context.State == WebSocketState.Open;
        public override bool CanSeek { get; } = false;
        public override bool CanWrite => context.State == WebSocketState.Open;

        public override long Length
        {
            get { throw new NotImplementedException(); }
        }

        public override long Position
        {
            get { throw new NotImplementedException(); }
            set { throw new NotImplementedException(); }
        }


        void SendCloseMessage()
        {
            if (context.State != WebSocketState.Open)
                return;

            var sendCancel = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            context.SendAsync(new ArraySegment<byte>(new byte[0]), WebSocketMessageType.Close, true, sendCancel.Token)
                .ConfigureAwait(false).GetAwaiter().GetResult();
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

    }
}