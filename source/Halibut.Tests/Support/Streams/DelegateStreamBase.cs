using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Tests.Support.Streams
{
    public abstract class DelegateStreamBase :  Stream
    {
        public abstract Stream Inner { get; }
        /**
         * New school async : Read
         **/
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => await Inner.ReadAsync(buffer, offset, count, cancellationToken);
#if !NETFRAMEWORK
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => await Inner.ReadAsync(buffer, cancellationToken);
#endif
        public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken) => await Inner.CopyToAsync(destination, bufferSize, cancellationToken);

        /**
         * New school async : Write
         **/
        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => await Inner.WriteAsync(buffer, offset, count, cancellationToken);
        public override async Task FlushAsync(CancellationToken cancellationToken) => await Inner.FlushAsync(cancellationToken);

#if !NETFRAMEWORK
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => await Inner.WriteAsync(buffer, cancellationToken);
        public override async ValueTask DisposeAsync() => await Inner.DisposeAsync();
#endif


        /**
         * Old school async : Read
         **/
        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state) => Inner.BeginRead(buffer, offset, count, callback, state);

        public override int EndRead(IAsyncResult asyncResult) => Inner.EndRead(asyncResult);
        
        /**
         * Old school async : Write
         **/
        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state) => Inner.BeginWrite(buffer, offset, count, callback, state);
        public override void EndWrite(IAsyncResult asyncResult) => Inner.EndWrite(asyncResult);

        /**
         * Sync IO: Read
         **/
        public override int ReadByte() => Inner.ReadByte();

        public override int Read(byte[] buffer, int offset, int count) => Inner.Read(buffer, offset, count);
        
#if !NETFRAMEWORK
        public override int Read(Span<byte> buffer) => Inner.Read(buffer);
#endif

        /**
         * Sync IO: Write
         **/
        public override void Write(byte[] buffer, int offset, int count) => Inner.Write(buffer, offset, count);
        public override void WriteByte(byte value) => Inner.WriteByte(value);

#if !NETFRAMEWORK
        public override void CopyTo(Stream destination, int bufferSize) => Inner.CopyTo(destination, bufferSize);
        public override void Write(ReadOnlySpan<byte> buffer) => Inner.Write(buffer);
#endif
        
        public override void Flush() => Inner.Flush();
        public override void Close() => Inner.Close();
        
        /**
         * ???
         **/
#if NETFRAMEWORK
        public override System.Runtime.Remoting.ObjRef CreateObjRef(Type requestedType) => Inner.CreateObjRef(requestedType);
        public override object? InitializeLifetimeService() => Inner.InitializeLifetimeService();
#endif
        
        public override long Seek(long offset, SeekOrigin origin) => Inner.Seek(offset, origin);
        public override void SetLength(long value) => Inner.SetLength(value);
        

        public override int ReadTimeout
        {
            get => Inner.ReadTimeout;
            set => Inner.ReadTimeout = value;
        }

        public override int WriteTimeout
        {
            get => Inner.WriteTimeout;
            set => Inner.WriteTimeout = value;
        }

        public override bool CanTimeout => Inner.CanTimeout;
        public override bool CanRead => Inner.CanRead;
        public override bool CanSeek => Inner.CanSeek;
        public override bool CanWrite => Inner.CanWrite;
        public override long Length => Inner.Length;

        public override long Position
        {
            get => Inner.Position;
            set => Inner.Position = value;
        }
    }
}