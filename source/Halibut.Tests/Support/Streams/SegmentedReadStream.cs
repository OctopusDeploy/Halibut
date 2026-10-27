using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Tests.Support.Streams
{
    /// <summary>
    /// A read-only stream that never returns data from more than one segment in a single read.
    ///
    /// Useful for mimicking data arriving over the network in separate writes (e.g. separate TLS records),
    /// where a read can return before the data from a later write has arrived.
    /// </summary>
    public class SegmentedReadStream : Stream
    {
        readonly Queue<byte[]> segments;
        byte[] currentSegment = Array.Empty<byte>();
        int currentSegmentOffset;

        public SegmentedReadStream(params byte[][] segments)
        {
            this.segments = new Queue<byte[]>(segments.Where(s => s.Length > 0));
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (currentSegmentOffset == currentSegment.Length)
            {
                if (segments.Count == 0)
                {
                    return 0;
                }

                currentSegment = segments.Dequeue();
                currentSegmentOffset = 0;
            }

            var bytesToRead = Math.Min(count, currentSegment.Length - currentSegmentOffset);
            Array.Copy(currentSegment, currentSegmentOffset, buffer, offset, bytesToRead);
            currentSegmentOffset += bytesToRead;
            return bytesToRead;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return Task.FromResult(Read(buffer, offset, count));
        }

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
    }
}
