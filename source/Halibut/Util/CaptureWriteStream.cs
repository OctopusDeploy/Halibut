using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Halibut.Util
{
    class CaptureWriteStream : Stream
    {
        readonly Stream inner;
        readonly int maxBytesToCapture;
        readonly List<byte> bytes;

        public CaptureWriteStream(Stream inner, int maxBytesToCapture)
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
            throw new NotSupportedException($"{nameof(CaptureWriteStream)} is intended for write-only streams.");
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException($"{nameof(CaptureReadStream)} does not support seeking.");
        }

        public override void SetLength(long value)
        {
            inner.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            inner.Write(buffer, offset, count);
            
            var spaceRemainingInBuffer = maxBytesToCapture - bytes.Count;
            var numBytesToCopyToCapture = Math.Min(count, spaceRemainingInBuffer);

            if (numBytesToCopyToCapture > 0)
            {
                bytes.AddRange(buffer.Skip(offset).Take(numBytesToCopyToCapture));
            }
        }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => inner.CanWrite;
        public override long Length => inner.Length;

        public override long Position
        {
            get => inner.Position;
            set => throw new NotSupportedException($"{nameof(CaptureReadStream)} is intended for write-only streams.");
        }
    }
}