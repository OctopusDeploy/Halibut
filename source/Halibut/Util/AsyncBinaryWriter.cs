using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Util
{
    class AsyncBinaryWriter : BinaryWriter
    {
        byte[] _buffer;

        public AsyncBinaryWriter(Stream stream)
            : base(stream)
        {
        }

        byte[] Buffer => _buffer ?? (_buffer = new byte[8]);

        public Task WriteAsync(long value, CancellationToken cancellationToken)
        {
            byte[] buffer = Buffer;
            buffer[0] = (byte)value;
            buffer[1] = (byte)(value >> 8);
            buffer[2] = (byte)(value >> 16);
            buffer[3] = (byte)(value >> 24);
            buffer[4] = (byte)(value >> 32);
            buffer[5] = (byte)(value >> 40);
            buffer[6] = (byte)(value >> 48);
            buffer[7] = (byte)(value >> 56);
            return OutStream.WriteAsync(buffer, 0, 8, cancellationToken);
        }

        public Task WriteAsync(byte[] buffer, CancellationToken cancellationToken)
        {
            return OutStream.WriteAsync(buffer, 0, buffer.Length, cancellationToken);
        }
    }
}