using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.Transport.Observability;

namespace Halibut.Transport.Protocol
{
    internal class ControlMessageReader
    {
        HalibutTimeoutsAndLimits halibutTimeoutsAndLimits;
        IControlMessageObserver controlMessageObserver;

        public ControlMessageReader(IControlMessageObserver controlMessageObserver, HalibutTimeoutsAndLimits halibutTimeoutsAndLimits)
        {
            this.halibutTimeoutsAndLimits = halibutTimeoutsAndLimits;
            this.controlMessageObserver = controlMessageObserver;
        }

        internal async Task<string> ReadUntilNonEmptyControlMessageAsync(Stream stream, CancellationToken cancellationToken)
        {
            controlMessageObserver.WaitingForControlMessage();
            while (true)
            {
                var line = await ReadControlMessageAsync(stream, cancellationToken);
                
                if (line.Length > 0)
                {
                    controlMessageObserver.ReceivedControlMessage(line);
                    return line;
                }
            }
        }

        internal async Task<string> ReadControlMessageAsync(Stream stream, CancellationToken cancellationToken)
        {
            using var timeoutCts = GetCancellationTokenSourceFromStreamReadTimeoutAsync(stream);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);
            var sb = new StringBuilder();

            while (true)
            {
                var nextByte = new byte[1];
                var read = await stream.ReadAsync(nextByte, 0, nextByte.Length, linkedCts.Token);
                if (read == 0)
                {
                    throw new EndOfStreamException();
                }

                // Control messages must end with \r\n and must not contain \r or \n
                if (nextByte[0] == '\r')
                {
                    read = await stream.ReadAsync(nextByte, 0, nextByte.Length, linkedCts.Token);
                    if (read == 0)
                    {
                        throw new EndOfStreamException();
                    }

                    if (nextByte[0] != '\n')
                    {
                        var byteAsHex = nextByte[0].ToString("X");
                        throw new Exception($"Unexpected byte after '\r' in control message: 0x{byteAsHex}");
                    }

                    // We have found the end of control message
                    return sb.ToString();
                }

                sb.Append((char)nextByte[0]);
            }
        }
        
        CancellationTokenSource GetCancellationTokenSourceFromStreamReadTimeoutAsync(Stream stream)
        {
            // TODO - ASYNC ME UP!
            // We should always be given a stream that can timeout.
            if (stream.CanTimeout)
            {
                return new CancellationTokenSource(stream.ReadTimeout);
            }

            return new CancellationTokenSource(halibutTimeoutsAndLimits.TcpClientTimeout.ReceiveTimeout); // Just default to a higher timeout, rather than be cancellation token none
        }
    }
}