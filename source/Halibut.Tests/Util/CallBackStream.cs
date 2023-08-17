using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Tests.Util
{
    public class CallBackStream : Stream
    {
        readonly Stream inner;
        Action<Stream> beforeReadAction = (innerStream) => { };
        Action<Stream> beforeWriteAction = (innerStream) => { };

        public CallBackStream(Stream inner)
        {
            this.inner = inner;
        }

        public CallBackStream WithBeforeRead(Action<Stream> beforeRead)
        {
            this.beforeReadAction += beforeRead;
            return this;
        }
        
        public CallBackStream WithBeforeWrite(Action<Stream> beforeRead)
        {
            this.beforeWriteAction += beforeRead;
            return this;
        }

        public override void Flush()
        {
            inner.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            beforeReadAction(inner);
            return inner.Read(buffer, offset, count);
        }
        
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            beforeReadAction(inner);
            return await inner.ReadAsync(buffer, offset, count, cancellationToken);
        }
        
        public override void Write(byte[] buffer, int offset, int count)
        {
            beforeWriteAction(inner);
            inner.Write(buffer, offset, count);
        }
        
        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            beforeWriteAction(inner);
            await inner.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return inner.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            inner.SetLength(value);
        }

        

        public override bool CanRead => inner.CanRead;

        public override bool CanSeek => inner.CanSeek;

        public override bool CanWrite => inner.CanWrite;

        public override long Length => inner.Length;

        public override long Position
        {
            get => inner.Position;
            set => inner.Position = value;
        }
    }
}