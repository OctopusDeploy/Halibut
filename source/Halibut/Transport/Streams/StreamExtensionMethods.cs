using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Transport.Streams
{
    public static class StreamExtensionMethods
    {
        // The NewLine and Encoding must not be changed as it will break backward compatibility.
        public static readonly string ControlMessageNewLine = "\r\n";
        static readonly Encoding Encoding = new UTF8Encoding(false);

        public static async Task WriteControlLineAsync(this Stream stream, string s, CancellationToken cancellationToken)
        {
            var bytes = Encoding.GetBytes(s + ControlMessageNewLine);
            await stream.WriteAsync(bytes, 0, bytes.Length, cancellationToken);
        }
        
        public static async Task WriteLineAsync(this Stream stream, string s, CancellationToken cancellationToken)
        {
            var bytes = Encoding.GetBytes(s + "\r\n");
            await stream.WriteAsync(bytes, 0, bytes.Length, cancellationToken);
        }

        public static async Task<int> ReadByteAsync(this Stream stream, CancellationToken cancellationToken)
        {
            byte[] b = new byte[1];
            int count = await stream.ReadAsync(b, 0, 1, cancellationToken);
            // Keep the same behaviour as ReadByte, which returns -1 if at the end of the stream
            if (count == 0) return -1;
            return b[0];
        }
        
        /// <summary>
        /// Writes a long to a stream in the same way that BinaryWriter does.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="value"></param>
        /// <param name="cancellationToken"></param>
        public static async Task WriteLongAsync(this Stream stream, long value, CancellationToken cancellationToken)
        {
            // An exact copy of:
            // System.IO.BinaryWriter.Write(long)
            var buffer = new byte[8];
            buffer[0] = (byte) value;
            buffer[1] = (byte) (value >> 8);
            buffer[2] = (byte) (value >> 16);
            buffer[3] = (byte) (value >> 24);
            buffer[4] = (byte) (value >> 32);
            buffer[5] = (byte) (value >> 40);
            buffer[6] = (byte) (value >> 48);
            buffer[7] = (byte) (value >> 56);
            await stream.WriteAsync(buffer, 0, 8, cancellationToken);
        }
        
        /// <summary>
        /// Net48 does not have a method which just writes all bytes of an array to a stream.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="bytes"></param>
        /// <param name="cancellationToken"></param>
        public static async Task WriteByteArrayAsync(this Stream stream, byte[] bytes, CancellationToken cancellationToken)
        {
            await stream.WriteAsync(bytes, 0, bytes.Length, cancellationToken);
        }
        
        /// <summary>
        /// Net48 does not have a method which just writes all bytes of an array to a stream.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="bytes"></param>
        /// <param name="cancellationToken"></param>
        public static void WriteByteArray(this Stream stream, byte[] bytes)
        {
            stream.Write(bytes, 0, bytes.Length);
        }
        
        public static async Task<byte[]> ReadBytesAsync(this Stream stream, int countToRead, CancellationToken cancellationToken)
        {
            var buffer = new byte[countToRead];
            int readSoFar = 0;
            while (buffer.Length > readSoFar)
            {
                int readLastTime = await stream.ReadAsync(buffer, readSoFar, buffer.Length - readSoFar, cancellationToken);
                if (readLastTime == 0)
                {
                    throw new EndOfStreamException();
                }

                readSoFar += readLastTime;
            }

            return buffer;
        }
        
        /// <summary>
        /// Reads a long (well Int64) from a stream in the same way as BinaryWriter does.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task<long> ReadInt64Async(this Stream stream, CancellationToken cancellationToken)
        {
            var buffer = await ReadBytesAsync(stream, 8, cancellationToken);
            // A copy of System.IO.BinaryReader.ReadInt64
            // ReSharper disable RedundantCast
            return (long) (uint) ((int) buffer[4] | (int) buffer[5] << 8 | (int) buffer[6] << 16 | (int) buffer[7] << 24) << 32 | (long) (uint) ((int) buffer[0] | (int) buffer[1] << 8 | (int) buffer[2] << 16 | (int) buffer[3] << 24);
            // ReSharper restore RedundantCast
        }
    }
}
