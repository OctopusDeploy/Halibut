using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Util
{
    class AsyncBinaryReader : BinaryReader
    {
        byte[] _buffer;
        byte[] Buffer => _buffer ?? (_buffer = new byte[16]);

        public AsyncBinaryReader(Stream input) : base(input)
        {
        }

        void EndOfStream()
        {
            throw new EndOfStreamException("Unable to read beyond the end of the stream.");
        }

        void FileNotOpen()
        {
            throw new ObjectDisposedException("Cannot access a closed file.");
        }

        async Task<byte[]> ReadBufferAsync(int size, CancellationToken cancellationToken)
        {
            var buffer = Buffer;
            int offset = 0;
            Stream stream = BaseStream;
            if (stream == null)
            {
                FileNotOpen();
            }

            do
            {
                int read = await BaseStream.ReadAsync(buffer, offset, size, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    EndOfStream();
                }

                offset += read;
                size -= read;
            } while (size > 0);

            return buffer;
        }

        public async Task<long> ReadInt64Async(CancellationToken cancellationToken)
        {
            var buffer = await ReadBufferAsync(8, cancellationToken).ConfigureAwait(false);
            uint lo = (uint)(buffer[0] | buffer[1] << 8 | buffer[2] << 16 | buffer[3] << 24);
            uint hi = (uint)(buffer[4] | buffer[5] << 8 | buffer[6] << 16 | buffer[7] << 24);
            return (long)hi << 32 | lo;
        }

        public Task<int> ReadAsync(byte[] buffer, int index, int count, CancellationToken cancellationToken)
        {
            Stream stream = BaseStream;
            if (stream == null)
            {
                FileNotOpen();
            }

            return stream.ReadAsync(buffer, index, count, cancellationToken);
        }

        public async Task<byte[]> ReadBytesAsync(int count, CancellationToken cancellationToken)
        {
            Stream stream = BaseStream;
            if (stream == null)
            {
                FileNotOpen();
            }

            if (count == 0)
            {
                return new byte[0];
            }

            byte[] result = new byte[count];
            int numRead = 0;
            do
            {
                int n = await stream.ReadAsync(result, numRead, count, cancellationToken).ConfigureAwait(false);
                if (n == 0)
                    break;
                numRead += n;
                count -= n;
            } while (count > 0);

            if (numRead != result.Length)
            {
                byte[] copy = new byte[numRead];
                result.CopyTo(copy, 0);
                return copy;
            }

            return result;
        }
    }
}