using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Transport.Protocol
{
    public class WebSocketStream : Stream
    {
        readonly WebSocket context;

        public WebSocketStream(WebSocket context)
        {
            this.context = context;
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

        public override int Read(byte[] buffer, int offset, int count)
        {
            var segment = new ArraySegment<byte>(buffer, offset, count);
            var recieveResult = context.ReceiveAsync(segment, CancellationToken.None)
                .ConfigureAwait(false).GetAwaiter().GetResult();
            return recieveResult.Count;
        }

        public async Task<string> ReadTextMessage()
        {
            var sb = new StringBuilder();
            var buffer = new ArraySegment<byte>(new byte[10000]);
            while (true)
            {
                var result = await context.ReceiveAsync(buffer, CancellationToken.None);
                if (result.MessageType != WebSocketMessageType.Text)
                    throw new Exception($"Encountered an unexpected message type {result.MessageType}");
                sb.Append(Encoding.UTF8.GetString(buffer.Array, 0, result.Count));

                if (result.EndOfMessage)
                    return sb.ToString();
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            context.SendAsync(new ArraySegment<byte>(buffer, offset, count), WebSocketMessageType.Binary, false, CancellationToken.None)
                .ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public Task WriteTextMessage(string message)
        {
            var buffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(message));
            return context.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                context.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}