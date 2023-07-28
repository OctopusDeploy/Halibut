using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;

namespace Halibut.Transport.Protocol
{
    internal class ControlMessageReader
    {
        internal string ReadUntilNonEmptyControlMessage(Stream stream)
        {
            while (true)
            {
                var line = ReadControlMessage(stream);
                if (line.Length > 0) return line;
            }
        }

        internal string ReadControlMessage(Stream stream)
        {
            StringBuilder sb = new StringBuilder();
            while (true)
            {
                var nextByte = stream.ReadByte();
                if (nextByte == -1) throw new EndOfStreamException();
                // Control messages must end with \r\n and must not contain \r or \n
                if (nextByte == '\r')
                {
                    nextByte = stream.ReadByte();
                    if (nextByte == -1)
                    {
                        throw new EndOfStreamException();
                    }

                    if (nextByte != '\n')
                    {
                        var byteAsHex = nextByte.ToString("X");
                        throw new Exception($"Unexpected byte after '\r' in control message: 0x{byteAsHex}");
                    }

                    // We have found the end of control message
                    return sb.ToString();
                }

                sb.Append((char) nextByte);
            }
        }

        internal async Task<string> ReadUntilNonEmptyControlMessageAsync(Stream stream)
        {
            while (true)
            {
                var line = await ReadControlMessageAsync(stream);
                if (line.Length > 0) return line;
            }
        }

        internal async Task<string> ReadControlMessageAsync(Stream stream)
        {
            using var cts = GetCancellationTokenSourceFromStreamReadTimeout(stream);
            StringBuilder sb = new StringBuilder();
            while (true)
            {
                var nextByte = new byte[1];
                var read = await stream.ReadAsync(nextByte, 0, nextByte.Length, cts.Token);
                if (read == 0) throw new EndOfStreamException();
                
                // Control messages must end with \r\n and must not contain \r or \n
                if (nextByte[0] == '\r')
                {
                    read = await stream.ReadAsync(nextByte, 0, nextByte.Length, cts.Token);
                    if (read == 0) throw new EndOfStreamException();

                    if (nextByte[0] != '\n')
                    {
                        var byteAsHex = nextByte[0].ToString("X");
                        throw new Exception($"Unexpected byte after '\r' in control message: 0x{byteAsHex}");
                    }

                    // We have found the end of control message
                    return sb.ToString();
                }

                sb.Append((char) nextByte[0]);
            }
        }

        static CancellationTokenSource GetCancellationTokenSourceFromStreamReadTimeout(Stream stream)
        {
            if (stream.CanTimeout)
            {
                return new CancellationTokenSource(stream.ReadTimeout);
            }

            return new CancellationTokenSource(HalibutLimits.TcpClientReceiveTimeout); // Just default to a higher timeout, rather than be cancellation token none
        }
    }
}