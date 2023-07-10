using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Transport.Observability
{
    public class ObservableStream : Stream
    {
        readonly Stream toObserve;

        public ObservableStream(Stream toObserve)
        {
            this.toObserve = toObserve;
        }

        public long BytesWritten { get; private set; }
        public long BytesRead { get; private set; }

        public override bool CanRead => toObserve.CanRead;
        public override bool CanWrite => toObserve.CanWrite;
        public override bool CanSeek => toObserve.CanSeek;
        public override bool CanTimeout => toObserve.CanTimeout;

        public override long Length => toObserve.Length;

        public override long Position
        {
            get => toObserve.Position;
            set => toObserve.Position = value;
        }

        public override int ReadTimeout
        {
            get => toObserve.ReadTimeout;
            set => toObserve.ReadTimeout = value;
        }

        public override int WriteTimeout
        {
            get => toObserve.WriteTimeout;
            set => toObserve.WriteTimeout = value;
        }
        
        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken) => toObserve.CopyToAsync(destination, bufferSize, cancellationToken);
        
        public override void Flush() => toObserve.Flush();

        public override Task FlushAsync(CancellationToken cancellationToken) => toObserve.FlushAsync(cancellationToken);

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state) => toObserve.BeginRead(buffer, offset, count, callback, state);
        
        public override int EndRead(IAsyncResult asyncResult) => toObserve.EndRead(asyncResult);

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var bytesRead = await toObserve.ReadAsync(buffer, offset, count, cancellationToken);

            BytesRead += bytesRead;

            return bytesRead;
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state) => toObserve.BeginWrite(buffer, offset, count, callback, state);

        public override void EndWrite(IAsyncResult asyncResult) => toObserve.EndWrite(asyncResult);

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await toObserve.WriteAsync(buffer, offset, count, cancellationToken);
            BytesWritten += count;
        }

        public override long Seek(long offset, SeekOrigin origin) => toObserve.Seek(offset, origin);

        public override void SetLength(long value) => toObserve.SetLength(value);

        public override int Read(byte[] buffer, int offset, int count)
        {
            var bytesRead = toObserve.Read(buffer, offset, count);
            
            BytesRead += bytesRead;

            return bytesRead;
        }
        
        public override void Write(byte[] buffer, int offset, int count)
        {
            toObserve.Write(buffer, offset, count);

            BytesWritten += count;
        }
    }
}