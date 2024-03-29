﻿using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Transport.Streams
{
    public enum OnDispose
    {
        DisposeInputStream,
        LeaveInputStreamOpen
    }

    public class ByteCountingStream : AsyncStream
    {
        readonly Stream countBytesFromStream;
        readonly OnDispose onDispose;

        public ByteCountingStream(Stream countBytesFromStream, OnDispose onDispose)
        {
            this.countBytesFromStream = countBytesFromStream;
            this.onDispose = onDispose;
        }

        public long BytesWritten { get; private set; }
        public long BytesRead { get; private set; }

        public override bool CanRead => countBytesFromStream.CanRead;
        public override bool CanWrite => countBytesFromStream.CanWrite;
        public override bool CanSeek => countBytesFromStream.CanSeek;
        public override bool CanTimeout => countBytesFromStream.CanTimeout;

        public override long Length => countBytesFromStream.Length;

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                if (onDispose == OnDispose.DisposeInputStream)
                {
                    countBytesFromStream.Dispose();
                }
            }
        }

        public override async ValueTask DisposeAsync()
        {
            if (onDispose == OnDispose.DisposeInputStream)
            {
                await countBytesFromStream.DisposeAsync();
            }
        }

        public override long Position
        {
            get => countBytesFromStream.Position;
            set => countBytesFromStream.Position = value;
        }

        public override int ReadTimeout
        {
            get => countBytesFromStream.ReadTimeout;
            set => countBytesFromStream.ReadTimeout = value;
        }

        public override int WriteTimeout
        {
            get => countBytesFromStream.WriteTimeout;
            set => countBytesFromStream.WriteTimeout = value;
        }

        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken) => countBytesFromStream.CopyToAsync(destination, bufferSize, cancellationToken);

        public override void Flush() => countBytesFromStream.Flush();

        public override Task FlushAsync(CancellationToken cancellationToken) => countBytesFromStream.FlushAsync(cancellationToken);

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var bytesRead = await countBytesFromStream.ReadAsync(buffer, offset, count, cancellationToken);

            BytesRead += bytesRead;

            return bytesRead;
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await countBytesFromStream.WriteAsync(buffer, offset, count, cancellationToken);
            BytesWritten += count;
        }

        public override long Seek(long offset, SeekOrigin origin) => countBytesFromStream.Seek(offset, origin);

        public override void SetLength(long value) => countBytesFromStream.SetLength(value);

        public override int Read(byte[] buffer, int offset, int count)
        {
            var bytesRead = countBytesFromStream.Read(buffer, offset, count);

            BytesRead += bytesRead;

            return bytesRead;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            countBytesFromStream.Write(buffer, offset, count);

            BytesWritten += count;
        }
    }
}