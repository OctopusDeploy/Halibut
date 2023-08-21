using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Transport.Streams
{
    public class ErrorRecordingStream : AsyncStream
    {
        readonly Stream innerStream;
        readonly bool closeInner;

        public ErrorRecordingStream(Stream innerStream, bool closeInner)
        {
            this.innerStream = innerStream;
            this.closeInner = closeInner;
        }

        public List<Exception> ReadExceptions { get; } = new();
        public List<Exception> WriteExceptions { get; } = new();

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
                if (read == 0 && count != 0) WasTheEndOfStreamEncountered = true;
                return read;
            }
            catch (Exception e)
            {
                ReadExceptions.Add(e);
                throw;
            }
            
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            try
            {
                int read = await innerStream.ReadAsync(buffer, offset, count, cancellationToken);
                if (read == 0 && count != 0) WasTheEndOfStreamEncountered = true;
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
        
        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
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

        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            try
            {
                await innerStream.FlushAsync(cancellationToken);
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
        
        protected override void Dispose(bool disposing)
        {
            if (closeInner && disposing)
            {
                innerStream.Dispose();
            }
        }

        public override async ValueTask DisposeAsync()
        {
            if (closeInner)
            {
                await innerStream.DisposeAsync();
            }
        }
    }
}