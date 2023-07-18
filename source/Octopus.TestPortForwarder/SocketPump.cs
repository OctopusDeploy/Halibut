using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace Octopus.TestPortForwarder
{

    public class SocketPump
    {
        public delegate bool IsPumpPaused();

        Stopwatch stopwatch = Stopwatch.StartNew();
        MemoryStream buffer = new MemoryStream();
        readonly TcpPump tcpPump;
        IsPumpPaused isPumpPaused;
        
        /// <summary>
        /// The pump will wait this long for new data to arrive before sending data.
        /// This means we will bundle up data and send it all in one request. The aim is
        /// to be able to find bugs where clients are reading more from the network than they
        /// should be.
        /// </summary>
        readonly TimeSpan sendDelay;
        
        /// <summary>
        /// The pumps will send all data read immediately, except for the last n bytes read.
        /// The last n bytes will be send a little later.
        /// This means if the pump reads "Hello" and this is set to one, then "hell" will be sent
        /// and ~10ms later "o" will be sent.
        ///
        /// This works with sendDelay, where sendDelay will "buffer" up more reads before sending and this will
        /// send all but the last N bytes of everything buffered and then ~10ms later send the last n bytes. Before
        /// returning to buffering up more reads.
        /// </summary>
        readonly int numberOfBytesToDelaySending;

        readonly IDataTransferObserver dataTransferObserver;
        readonly ILogger logger;

        public SocketPump(TcpPump tcpPump, IsPumpPaused isPumpPaused, TimeSpan sendDelay, IDataTransferObserver dataTransferObserver, int numberOfBytesToDelaySending, ILogger logger)
        {
            this.tcpPump = tcpPump;
            this.isPumpPaused = isPumpPaused;
            this.sendDelay = sendDelay;
            this.dataTransferObserver = dataTransferObserver;
            this.numberOfBytesToDelaySending = numberOfBytesToDelaySending;
            this.logger = logger;
        }

        public async Task<SocketStatus> PumpBytes(Socket readFrom, Socket writeTo, CancellationToken cancellationToken)
        {
            await PausePump(cancellationToken);

            // Only read if we have nothing to send or if data exists
            if (readFrom.Available > 0 || buffer.Length == 0)
            {
                var receivedByteCount = await ReadFromSocket(readFrom, buffer, cancellationToken);
                await PausePump(cancellationToken);
                if (receivedByteCount == 0) return SocketStatus.SOCKET_CLOSED;
                stopwatch = Stopwatch.StartNew();
            }

            await PausePump(cancellationToken);

            bool shouldWrite = sendDelay == TimeSpan.Zero 
                || (readFrom.Available == 0 && stopwatch.Elapsed >= sendDelay) || buffer.Length > 100 * 1024 * 1024; 
            if (shouldWrite)
            {
                // Send the data
                dataTransferObserver.WritingData(tcpPump, buffer);

                await PausePump(cancellationToken);
                await WriteToSocketDelayingSendingTheLastNBytes(writeTo, buffer.GetBuffer(), (int)buffer.Length, numberOfBytesToDelaySending, cancellationToken);
                buffer.SetLength(0);
            }
            else
            {
                await Task.Delay(1, cancellationToken);
            }

            return SocketStatus.SOCKET_OPEN;
        }

        internal async Task PausePump(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            while (isPumpPaused())
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            }
        }
        
        static async Task WriteToSocketDelayingSendingTheLastNBytes(Socket writeTo, byte[] buffer, int bufferLength, int delaySendingLastNBytes, CancellationToken cancellationToken)
        {
            var howMuchToSend = bufferLength - delaySendingLastNBytes;
            if(howMuchToSend < 0) howMuchToSend = bufferLength;
            var sent = await WriteToSocket(writeTo, buffer, 0, howMuchToSend, cancellationToken);
            if (howMuchToSend < bufferLength)
            {
                await Task.Delay(10);
                sent += await WriteToSocket(writeTo, buffer, howMuchToSend, bufferLength - howMuchToSend, cancellationToken);
            }
            if (sent != bufferLength)
            {
                throw new Exception($"Was supported to send {bufferLength} but sent {sent}");
            }
        }
        
        static async Task<int> WriteToSocket(Socket writeTo, byte[] buffer, int initialOffset, int totalBytesToSend, CancellationToken cancellationToken)
        {
            var toSend = new ArraySegment<byte>(buffer, initialOffset, totalBytesToSend);

            var offset = 0;
            while (totalBytesToSend - offset > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var outputBuffer = toSend.Slice(offset, totalBytesToSend - offset);

#if DOES_NOT_SUPPORT_CANCELLATION_ON_SOCKETS
                var sendAsyncCancellationTokenSource = new CancellationTokenSource();
                using (cancellationToken.Register(() => sendAsyncCancellationTokenSource.Cancel()))
                {
                    var cancelTask = sendAsyncCancellationTokenSource.Token.AsTask<int>();
                    var actionTask = writeTo.SendAsync(outputBuffer, SocketFlags.None);

                    offset += await (await Task.WhenAny(actionTask, cancelTask).ConfigureAwait(false)).ConfigureAwait(false);
                }
#else
                offset += await writeTo.SendAsync(outputBuffer, SocketFlags.None, cancellationToken).ConfigureAwait(false);
#endif

                cancellationToken.ThrowIfCancellationRequested();
            }

            return offset;
        }

        static async Task<int> ReadFromSocket(Socket readFrom, MemoryStream memoryStream, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var inputBuffer = new byte[readFrom.ReceiveBufferSize];
            ArraySegment<byte> inputBufferArraySegment = new ArraySegment<byte>(inputBuffer);

#if DOES_NOT_SUPPORT_CANCELLATION_ON_SOCKETS
            int receivedByteCount;
            var receiveAsyncCancellationTokenSource = new CancellationTokenSource();
            using (cancellationToken.Register(() => receiveAsyncCancellationTokenSource.Cancel()))
            {
                var cancelTask = receiveAsyncCancellationTokenSource.Token.AsTask<int>();
                var actionTask = readFrom.ReceiveAsync(inputBufferArraySegment, SocketFlags.None);

                receivedByteCount = await (await Task.WhenAny(actionTask, cancelTask).ConfigureAwait(false)).ConfigureAwait(false);
            }
#else
            var receivedByteCount = await readFrom.ReceiveAsync(inputBufferArraySegment, SocketFlags.None, cancellationToken).ConfigureAwait(false);
#endif

            cancellationToken.ThrowIfCancellationRequested();

            memoryStream.Write(inputBuffer, 0, receivedByteCount);
            return receivedByteCount;
        }


        public enum SocketStatus
        {
            SOCKET_CLOSED,
            SOCKET_OPEN
        }
    }

#if NET48
    public static class ArraySegmentExtensionMethods
    {
        public static ArraySegment<T> Slice<T>(this ArraySegment<T> arraySegment, int index, int count)
        {
            return new ArraySegment<T>(arraySegment.Array!, arraySegment.Offset + index, count);
        }
    }
#endif
}