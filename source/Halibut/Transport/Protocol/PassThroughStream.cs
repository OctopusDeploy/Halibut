using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Transport.Protocol
{
    /// <summary>
    /// Provides a way to pass calls through to another stream.
    /// Does nothing interesting by itself.
    /// Override specific methods to customise.
    /// </summary>
    public class PassThroughStream : Stream
    {
        protected readonly Stream UnderlyingStream;

        public PassThroughStream(Stream underlyingStream)
        {
            UnderlyingStream = underlyingStream;
        }

        public override void Flush() => UnderlyingStream.Flush();

        public override int Read(byte[] buffer, int offset, int count)
        {
            return UnderlyingStream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return UnderlyingStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            UnderlyingStream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            UnderlyingStream.Write(buffer, offset, count);
        }

        public override bool CanRead => UnderlyingStream.CanRead;
        public override bool CanSeek => UnderlyingStream.CanSeek;
        public override bool CanWrite => UnderlyingStream.CanWrite;
        public override long Length => UnderlyingStream.Length;

        public override long Position
        {
            get => UnderlyingStream.Position;
            set => UnderlyingStream.Position = value;
        }

        public override void Close()
        {
            UnderlyingStream.Close();
        }

        protected override void Dispose(bool disposing)
        {
            UnderlyingStream.Dispose();
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return UnderlyingStream.BeginRead(buffer, offset, count, callback, state);
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return UnderlyingStream.BeginWrite(buffer, offset, count, callback, state);
        }

        public override bool CanTimeout => UnderlyingStream.CanTimeout;

        public override int EndRead(IAsyncResult asyncResult)
        {
            return UnderlyingStream.EndRead(asyncResult);
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            UnderlyingStream.EndWrite(asyncResult);
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return UnderlyingStream.FlushAsync(cancellationToken);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return UnderlyingStream.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public override int ReadByte()
        {
            return UnderlyingStream.ReadByte();
        }

        public override int ReadTimeout
        {
            get => UnderlyingStream.ReadTimeout;
            set => UnderlyingStream.ReadTimeout = value;
        }
    }

    public class TruncateStream : PassThroughStream
    {
        readonly long truncateTo;
        long bytesWritten = 0;
        
        public TruncateStream(Stream underlyingStream, long truncateTo) : base(underlyingStream)
        {
            this.truncateTo = truncateTo;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            var numBytesToWrite = (int)Math.Min(truncateTo - bytesWritten, count);
            if (numBytesToWrite > 0)
            {
                UnderlyingStream.Write(buffer, offset, numBytesToWrite);
            }

            bytesWritten += numBytesToWrite;
        }
    }
    
    /// <summary>
    /// Fiddles with some of the bytes in a stream.
    /// </summary>
    public class CorruptBytesStream : PassThroughStream
    {
        public CorruptBytesStream(Stream underlyingStream) : base(underlyingStream)
        {
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            for (var i = 3; i < buffer.Length; i += 10)
            {
                buffer[i] = (byte)~buffer[i];
            }
            
            base.Write(buffer, offset, count);
        }
    }
}