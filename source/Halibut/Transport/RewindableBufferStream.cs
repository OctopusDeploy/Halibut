using System;
using System.IO;

namespace Halibut.Transport
{
    class RewindableBufferStream : Stream, IRewindableBuffer
    {
        readonly Stream baseStream;
        readonly byte[] rewindBuffer;
        bool rewindBufferPopulated;
        int rewindBufferOffset;
        int rewindBufferCount;
        bool rewindEnabled;

        public RewindableBufferStream(Stream baseStream, int rewindBufferSize = 16384)
        {
            this.baseStream = baseStream;
            rewindBuffer = new byte[rewindBufferSize];
        }

        public override void Flush() => baseStream.Flush();

        public void StartRewindBuffer()
        {
            if (rewindEnabled)
            {
                throw new NotSupportedException("Rewind buffer has already been started.");
            }

            ResetRewindBuffer();
            rewindEnabled = true;
        }

        /// <summary>
        /// Stops read data being added to the rewind buffer, and rewinds the buffer.
        /// </summary>
        /// <param name="rewindCount">The number of bytes to rewind.</param>
        /// <exception cref="ArgumentOutOfRangeException"><see cref="rewindCount"/> must be a positive number and less than the number of bytes read since <see cref="StartRewindBuffer"/> was called.</exception>
        /// <exception cref="NotSupportedException"><see cref="StartRewindBuffer"/> has not been called.</exception>
        public void FinishRewindBuffer(long rewindCount)
        {
            if (!rewindEnabled)
            {
                throw new NotSupportedException("The rewind buffer has not been started.");
            }

            if (rewindCount < 0 || rewindCount > rewindBufferCount) throw new ArgumentOutOfRangeException(nameof(rewindCount));

            RewindBuffer((int)rewindCount);
            rewindEnabled = false;
        }

        /// <summary>
        /// Stops read data being added to the rewind buffer. Does not rewind the buffer.
        /// </summary>
        /// <exception cref="NotSupportedException"><see cref="StartRewindBuffer"/> has not been called.</exception>
        public void CancelRewindBuffer()
        {
            if (!rewindEnabled)
            {
                throw new NotSupportedException("The rewind buffer has not been started.");
            }

            ResetRewindBuffer();
            rewindEnabled = false;
        }

        /// <summary>
        /// Reads a sequence bytes from the stream. If the buffer has been rewound, only the rewound bytes are returned.
        /// </summary>
        /// <param name="buffer"><inheritdoc cref="Stream.Read" path="/param[@name='buffer']"/></param>
        /// <param name="offset"><inheritdoc cref="Stream.Read" path="/param[@name='offset']"/></param>
        /// <param name="count"><inheritdoc cref="Stream.Read" path="/param[@name='count']"/></param>
        /// <returns><inheritdoc cref="Stream.Read" path="/returns"/></returns>
        public override int Read(byte[] buffer, int offset, int count)
        {
            var rewoundCount = ReadFromRewindBuffer(buffer, offset, count);
            
            // Do not attempt to read from the base stream if the buffer has been partially filled
            // from the rewind buffer. This is for safety, because if the base stream is a type of NetworkStream,
            // it will kill the TCP connection (for reasons unknown....).
            if (rewoundCount > 0)
            {
                return rewoundCount;
            }

            var baseCount = baseStream.Read(buffer, rewoundCount, count);
            WriteToRewindBuffer(buffer, offset, baseCount);
            return rewoundCount + baseCount;
        }

        public override long Seek(long offset, SeekOrigin origin)
            => throw new NotSupportedException();

        public override void SetLength(long value)
            => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            baseStream.Write(buffer, offset, count);
        }
            

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;

        public override long Length
            => throw new NotSupportedException();

        public override long Position
        {
            get => baseStream.Position;
            set => throw new NotSupportedException($"You probably want {nameof(FinishRewindBuffer)}.");
        }
        void RewindBuffer(int rewindSize)
        {
            if (rewindSize > rewindBufferCount)
            {
                throw new ArgumentException($"Cannot rewind farther than what has been read since {nameof(StartRewindBuffer)} was called.");
            }

            if (rewindBufferOffset - rewindSize < 0)
            {
                throw new ArgumentException($"Cannot rewind before the start of data read since {nameof(StartRewindBuffer)} was called.");
            }

            rewindBufferOffset -= rewindSize;
        }

        /// <summary>
        /// Fill the given buffer with data from the rewind buffer.
        /// </summary>
        /// <returns>Number of bytes written to the given buffer.</returns>
        int ReadFromRewindBuffer(byte[] buffer, int offset, int count)
        {
            if (!rewindBufferPopulated || rewindBufferOffset >= rewindBufferCount)
            {
                return 0;
            }

            var readCount = Math.Min(count, rewindBufferCount - rewindBufferOffset);
            Buffer.BlockCopy(rewindBuffer, rewindBufferOffset, buffer, offset, readCount);
            rewindBufferOffset += readCount;
            return readCount;
        }

        void WriteToRewindBuffer(byte[] inputBuffer, int offset, int count)
        {
            if (!rewindEnabled || count <= 0)
            {
                return;
            }

            // If writing the requested count would overfill the rewind buffer, reset the rewind buffer to the beginning.
            if (rewindBufferCount + count > rewindBuffer.Length)
            {
                ResetRewindBuffer();
            }

            Buffer.BlockCopy(inputBuffer, offset, rewindBuffer, rewindBufferOffset, count);
            rewindBufferCount += count;
            rewindBufferOffset += count;
            rewindBufferPopulated = true;
        }

        void ResetRewindBuffer()
        {
            rewindBufferOffset = 0;
            rewindBufferCount = 0;
        }
    }
}