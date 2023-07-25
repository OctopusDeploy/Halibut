using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Transport.Streams
{
    public class ErrorRecordingStream : Stream
    {
        Stream innerStream;

        public ErrorRecordingStream(Stream innerStream)
        {
            this.innerStream = innerStream;
        }

        public List<Exception> ReadExceptions { get; } = new List<Exception>();
        public List<Exception> WriteExceptions { get; } = new List<Exception>();

        public bool WasTheEndOfStreamEncountered { get; private set; } = false;

        public override void Flush()
        {
            innerStream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            try
            {
                int read = innerStream.Read(buffer, offset, count);
                if (read == 0) WasTheEndOfStreamEncountered = true;
                return read;
            }
            catch (Exception e)
            {
                ReadExceptions.Add(e);
                throw;
            }
            
        }

        public async override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            try
            {
                int read = await innerStream.ReadAsync(buffer, offset, count, cancellationToken);
                if (read == 0) WasTheEndOfStreamEncountered = true;
                return read;
            }
            catch (Exception e)
            {
                ReadExceptions.Add(e);
                throw;
            }
            
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return innerStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            innerStream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            try
            {
                innerStream.Write(buffer, offset, count);
            }
            catch (Exception e)
            {
                WriteExceptions.Add(e);
                throw;
            }
        }
        
        public async override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            try
            {
                await innerStream.WriteAsync(buffer, offset, count, cancellationToken);
            }
            catch (Exception e)
            {
                WriteExceptions.Add(e);
                throw;
            }
        }

        public override bool CanRead => innerStream.CanRead;

        public override bool CanSeek => innerStream.CanSeek;

        public override bool CanWrite => innerStream.CanWrite;

        public override long Length => innerStream.Length;

        public override long Position
        {
            get => innerStream.Position;
            set => innerStream.Position = value;
        }
    }
}