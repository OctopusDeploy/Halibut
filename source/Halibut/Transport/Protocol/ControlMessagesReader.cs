using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Halibut.Transport.Protocol
{
    public class ControlMessagesReader
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
                var line = stream.ReadByte();
                if (line == -1) throw new EndOfStreamException();
                if (line == '\r')
                {
                    line = stream.ReadByte();
                    if (line == -1)
                    {
                        throw new EndOfStreamException();
                    }

                    if (line != '\n')
                    {
                        throw new Exception($"It is not clear what should be done with this character: dec value {line}");
                    }

                    // We have found the end of the line, ie we have found \r\n
                    return sb.ToString();
                }

                sb.Append((char) line);
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
            StringBuilder sb = new StringBuilder();
            while (true)
            {
                var line = new byte[1];
                var read = await stream.ReadAsync(line, 0, line.Length);
                if (read == 0) throw new EndOfStreamException();
                if (line[0] == '\r')
                {
                    read = await stream.ReadAsync(line, 0, line.Length);
                    if (read == 0) throw new EndOfStreamException();

                    if (line[0] != '\n')
                    {
                        throw new Exception($"It is not clear what should be done with this character: dec value {line}");
                    }

                    // We have found the end of control message
                    return sb.ToString();
                }

                sb.Append((char) line[0]);
            }
        }
    }
}