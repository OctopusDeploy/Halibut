using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Transport.Streams
{
    public static class StreamExtensionMethods
    {
        // The NewLine and Encoding must not be changed as it will break backward compatibility.
        static readonly string NewLine = "\r\n";
        static readonly Encoding Encoding = new UTF8Encoding(false);

        public static async Task WriteLineAsync(this Stream stream, string s, CancellationToken cancellationToken)
        {
            var bytes = Encoding.GetBytes(s + NewLine);
            await stream.WriteAsync(bytes, 0, bytes.Length, cancellationToken);
        }

        public static async Task WriteLineAsync(this Stream stream, CancellationToken cancellationToken)
        {
            await stream.WriteLineAsync(string.Empty, cancellationToken);
        }
    }
}
