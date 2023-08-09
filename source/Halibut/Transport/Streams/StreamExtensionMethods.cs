using System;
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
    }
}
