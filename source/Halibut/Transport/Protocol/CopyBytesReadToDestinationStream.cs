using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Transport.Protocol
{
    public class CopyBytesReadToDestinationStream : Stream
    {
        readonly Stream source;
        readonly Stream destination;

        public CopyBytesReadToDestinationStream(Stream source, Stream destination)
        {
            this.source = source;
            this.destination = destination;
        }
        
        public override int Read(byte[] buffer, int offset, int count)
        {
            var bytesRead = source.Read(buffer, offset, count);

            if (bytesRead == 0) return 0;

            destination.Write(buffer, offset, bytesRead);

            return bytesRead;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var bytesRead = await source.ReadAsync(buffer, offset, count, cancellationToken);

            if (bytesRead == 0) return 0;

            await destination.WriteAsync(buffer, offset, bytesRead, cancellationToken);

            return bytesRead;
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override bool CanRead => source.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => source.Length;

        public override long Position
        {
            get => source.Position;
            set => throw new NotImplementedException();
        }
    }
}