using System;
using System.IO;
using System.Threading.Tasks;
using System.Threading;

namespace Halibut.Transport.Protocol
{
    public class FileOverflowMemoryStream : Stream
    {
        readonly long overflowLimitBytes;
        readonly MemoryStream memoryStream = new ();
        FileStream overflowFileStream = null;
        Stream currentStream;

        public FileOverflowMemoryStream(long overflowLimitBytes)
        {
            this.overflowLimitBytes = overflowLimitBytes;
            currentStream = memoryStream;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (overflowFileStream is null && (memoryStream.Position + count) > overflowLimitBytes)
            {
                overflowFileStream = File.Create(Guid.NewGuid().ToString());

                var currentPosition = memoryStream.Position;

                memoryStream.Position = 0;
                memoryStream.CopyTo(overflowFileStream);

                overflowFileStream.Position = currentPosition;

                currentStream = overflowFileStream;
            }

            currentStream.Write(buffer, offset, count);
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (overflowFileStream is null && (memoryStream.Position + count) > overflowLimitBytes)
            {
                overflowFileStream = File.Create(Guid.NewGuid().ToString());

                var currentPosition = memoryStream.Position;

                memoryStream.Position = 0;
                await memoryStream.CopyToAsync(overflowFileStream, 81920, cancellationToken);

                overflowFileStream.Position = currentPosition;

                currentStream = overflowFileStream;
            }

            await currentStream.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override bool CanRead => currentStream.CanRead;
        public override bool CanSeek => currentStream.CanSeek;
        public override bool CanWrite => currentStream.CanWrite;

        protected override void Dispose(bool disposing)
        {
            try
            {
                memoryStream.Dispose();

                if (overflowFileStream is not null)
                {
                    var fileName = overflowFileStream.Name;

                    overflowFileStream.Dispose();
                    overflowFileStream = null;

                    File.Delete(fileName);
                }
            }
            finally
            {
                // Call base.Close() to cleanup async IO resources
                base.Dispose(disposing);
            }
        }
        
        public override void Flush() => currentStream.Flush();
        public override long Length => currentStream.Length;

        public override long Position
        {
            get => currentStream.Position;
            set => currentStream.Position = value;
        }

        public override int Read(byte[] buffer, int offset, int count) => currentStream.Read(buffer, offset, count);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => currentStream.ReadAsync(buffer, offset, count, cancellationToken);

        public override int ReadByte() => currentStream.ReadByte();
        
        public override long Seek(long offset, SeekOrigin loc) => currentStream.Seek(offset, loc);
        
        public override void SetLength(long value) => currentStream.SetLength(value);
        
        
    }
}