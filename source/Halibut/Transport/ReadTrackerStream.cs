using System.IO;

namespace Halibut.Transport
{
    /// <summary>
    /// Track the number of bytes read from a decorated stream. The decorated stream is not disposed.
    /// </summary>
    class ReadTrackerStream : Stream
    {
        readonly Stream baseStream;

        public ReadTrackerStream(Stream stream)
        {
            baseStream = stream;
        }

        /// <summary>
        /// The total count of actual bytes read from the over this instance's lifetime.
        /// </summary>
        public long TotalBytesRead { get; private set; }

        public override void Flush() => baseStream.Flush();

        public override int Read(byte[] buffer, int offset, int count)
        {
            var result = baseStream.Read(buffer, offset, count);
            TotalBytesRead += result;
            return result;
        }

        public override long Seek(long offset, SeekOrigin origin) => baseStream.Seek(offset, origin);
        public override void SetLength(long value) => baseStream.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => baseStream.Write(buffer, offset, count);

        public override bool CanRead => baseStream.CanRead;
        public override bool CanSeek => baseStream.CanSeek;
        public override bool CanWrite => baseStream.CanWrite;
        public override long Length => baseStream.Length;

        public override long Position
        {
            get => baseStream.Position;
            set => baseStream.Position = value;
        }
    }
}
