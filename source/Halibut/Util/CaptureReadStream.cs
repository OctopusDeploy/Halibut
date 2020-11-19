using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Halibut.Util
{
    class CaptureReadStream : Stream
    {
        readonly Stream inner;
        readonly int maxBytesToCapture;
        readonly List<byte> bytes;

        public CaptureReadStream(Stream inner, int maxBytesToCapture)
        {
            this.inner = inner;
            this.maxBytesToCapture = maxBytesToCapture;
            bytes = new List<byte>(maxBytesToCapture);
        }

        public byte[] GetBytes() => bytes.ToArray();

        public override void Flush()
        {
            inner.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var bytesRead = inner.Read(buffer, offset, count);
            var spaceRemainingInBuffer = maxBytesToCapture - bytes.Count;
            var numBytesToCopyToCapture = Math.Min(bytesRead, spaceRemainingInBuffer);

            if (numBytesToCopyToCapture > 0)
            {
                bytes.AddRange(buffer.Skip(offset).Take(numBytesToCopyToCapture));
            }

            return bytesRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException($"{nameof(CaptureReadStream)} does not support seeking.");
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException($"{nameof(CaptureReadStream)} is intended for read-only streams.");
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException($"{nameof(CaptureReadStream)} is intended for read-only streams.");
        }

        public override bool CanRead => inner.CanRead;

        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => inner.Length;
        public override long Position
        {
            get => inner.Position;
            set => throw new NotSupportedException($"{nameof(CaptureReadStream)} is intended for read-only streams.");
        }
    }
}