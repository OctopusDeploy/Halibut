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
    public static class WebSocketExtensionMethods
    {
        static readonly TimeSpan SendCancelTimeout = TimeSpan.FromSeconds(1);

        public static async Task<string?> ReadTextMessage(this WebSocket context, TimeSpan timeout, CancellationToken cancellationToken)
        {
            context.AssertCanReadOrWrite();
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

        public static async Task WriteTextMessage(this WebSocket context, string message, TimeSpan timeout, CancellationToken cancellationToken)
        {
            context.AssertCanReadOrWrite();
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

        public static void AssertCanReadOrWrite(this WebSocket context)
        {
            if (context.CloseStatus.HasValue)
            {
                throw new Exception("Remote endpoint closed the stream");
            }
        }
    }
}