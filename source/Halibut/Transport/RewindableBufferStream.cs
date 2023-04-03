using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Transport
{
    class RewindableBufferStream : Stream, IRewindableBuffer
    {
        public readonly Stream baseStream;
        readonly byte[] rewindBuffer;
        bool rewindBufferPopulated;
        int rewindBufferOffset;
        int rewindBufferCount;
        bool rewindEnabled;

        public RewindableBufferStream(Stream baseStream, int rewindBufferSize = 8192)
        {
            this.baseStream = baseStream;
            rewindBuffer = new byte[rewindBufferSize];
        }

        public override void Flush() => baseStream.Flush();

        /// <summary>
        /// Keep a rewind buffer of the latest <see cref="Stream.Read"/> on the underlying stream.
        /// </summary>
        public void StartBuffer()
        {
            if (rewindEnabled)
            {
                throw new NotSupportedException("Rewind buffer has already been started.");
            }

            ResetRewindBuffer();
            rewindEnabled = true;
        }

        /// <summary>
        /// Stops data being added to the rewind buffer, and rewinds the buffer.
        /// </summary>
        /// <param name="rewindCount">The number of bytes to rewind.</param>
        /// <exception cref="ArgumentOutOfRangeException"><see cref="rewindCount"/> must be a positive number and less than the number of bytes read since <see cref="StartBuffer"/> was called.</exception>
        /// <exception cref="NotSupportedException"><see cref="StartBuffer"/> has not been called.</exception>
        public void FinishAndRewind(long rewindCount)
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
        /// <exception cref="NotSupportedException"><see cref="StartBuffer"/> has not been called.</exception>
        public void CancelBuffer()
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
            // from the rewind buffer. This is for safety, so the Halibut protocol doesn't accidentally
            // consume bytes destined for a subsequent operation.
            if (rewoundCount > 0)
            {
                return rewoundCount;
            }

            var baseCount = baseStream.Read(buffer, offset, count);
            WriteToRewindBuffer(buffer, offset, baseCount);
            return baseCount;
        }

        /// <summary>
        /// Reads a sequence of bytes from the stream asynchronously. If the buffer has been rewound, only the rewound bytes are returned.
        /// </summary>
        /// <param name="buffer"><inheritdoc/></param>
        /// <param name="offset"><inheritdoc/></param>
        /// <param name="count"><inheritdoc/></param>
        /// <param name="cancellationToken"><inheritdoc/></param>
        /// <returns><inheritdoc/></returns>
        public async override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var rewoundCount = ReadFromRewindBuffer(buffer, offset, count);

            // Do not attempt to read from the base stream if the buffer has been partially filled
            // from the rewind buffer. This is for safety, so the Halibut protocol doesn't accidentally
            // consume bytes destined for a subsequent operation.
            if (rewoundCount > 0)
            {
                return rewoundCount;
            }

            var baseCount = await baseStream.ReadAsync(buffer, offset, count, cancellationToken);
            WriteToRewindBuffer(buffer, offset, baseCount);
            return baseCount;
        }

        public override long Seek(long offset, SeekOrigin origin)
            => throw new NotSupportedException();

        public override void SetLength(long value)
            => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            baseStream.Write(buffer, offset, count);
        }

        public async override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await baseStream.WriteAsync(buffer, offset, count, cancellationToken);
        }


        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;

        public override bool CanTimeout => baseStream.CanTimeout;
        
        public override int ReadTimeout
        {
            get => baseStream.ReadTimeout;
            set => baseStream.ReadTimeout = value;
        }

        public override int WriteTimeout
        {
            get => baseStream.WriteTimeout;
            set => baseStream.WriteTimeout = value;
        }

        public override long Length
            => throw new NotSupportedException();

        public override long Position
        {
            get => baseStream.Position;
            set => throw new NotSupportedException($"You probably want {nameof(FinishAndRewind)}.");
        }
        void RewindBuffer(int rewindSize)
        {
            if (rewindSize > rewindBufferCount)
            {
                throw new ArgumentException($"Cannot rewind farther than what has been read since {nameof(StartBuffer)} was called.");
            }

            if (rewindBufferOffset - rewindSize < 0)
            {
                throw new ArgumentException($"Cannot rewind before the start of data read since {nameof(StartBuffer)} was called.");
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
            rewindBufferPopulated = false;
        }
    }
}
