using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Tests.Support.Streams;
using Halibut.Tests.Util;
using Halibut.Transport.Streams;
using NUnit.Framework;

namespace Halibut.Tests.Transport.Streams
{
    public class AsyncStreamFixture : StreamWrapperSupportsAsyncIOFixture
    {
        protected override AsyncStream WrapStream(Stream stream) => new NoOpAsyncStream(stream);
    }

    public class NoOpAsyncStream : AsyncStream
    {
        readonly Stream stream;

        public NoOpAsyncStream(Stream stream)
        {
            this.stream = stream;
        }

        public override void Flush()
        {
            stream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return stream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return stream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            stream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            stream.Write(buffer, offset, count);
        }

        public override bool CanRead => stream.CanRead;

        public override bool CanSeek => stream.CanSeek;

        public override bool CanWrite => stream.CanWrite;

        public override long Length => stream.Length;

        public override long Position
        {
            get => stream.Position;
            set => stream.Position = value;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return stream.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return stream.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return stream.FlushAsync(cancellationToken);
        }

        public override ValueTask DisposeAsync()
        {
            return stream.DisposeAsync();
        }
    }

    public abstract class StreamWrapperSupportsAsyncIOFixture : BaseTest
    {
        protected abstract AsyncStream WrapStream(Stream stream);

        [Test]
        public async Task ApmBeginReadUsesAsyncMethods()
        {
            var memoryStream = new MemoryStream();
            memoryStream.WriteString("Hello");
            memoryStream.Position = 0;

            var noSyncIoStream = new NoSyncIoStream(memoryStream);

            await using (var sut = WrapStream(noSyncIoStream))
            {
                var buffer = new byte[100];
                var read = await Task<int>.Factory.FromAsync(sut.BeginRead, sut.EndRead, buffer, 0, buffer.Length, null);

                read.Should().Be(5);

                Encoding.UTF8.GetString(buffer, 0, 5)
                    .Should()
                    .Be("Hello");
            }

            noSyncIoStream.SyncCalls.Should().BeEmpty();
        }

        [Test]
        public async Task ApmBeginWriteUsesAsyncMethods()
        {
            var memoryStream = new MemoryStream();

            var noSyncIoStream = new NoSyncIoStream(memoryStream);

            await using (var sut = WrapStream(noSyncIoStream))
            {
                var buffer = "Hello".GetBytesUtf8();
                await Task.Factory.FromAsync(sut.BeginWrite, sut.EndWrite, buffer, 0, buffer.Length, null);

                await sut.FlushAsync(CancellationToken);

                memoryStream.Position = 0;

                Encoding.UTF8.GetString(memoryStream.ToArray())
                    .Should()
                    .Be("Hello");
            }

            noSyncIoStream.SyncCalls.Should().BeEmpty();
        }

        [Test]
        public async Task FlushAsyncUsesFlushAsync()
        {
            var memoryStream = new MemoryStream();

            var noSyncIoStream = new NoSyncIoStream(memoryStream);

            await using (var sut = WrapStream(noSyncIoStream))
            {
                await sut.WriteByteArrayAsync("Hello".GetBytesUtf8(), CancellationToken);
                await sut.FlushAsync(CancellationToken);

                memoryStream.Position = 0;

                Encoding.UTF8.GetString(memoryStream.ToArray())
                    .Should()
                    .Be("Hello");
            }

            noSyncIoStream.SyncCalls.Should().BeEmpty();
        }

        [Test]
        public async Task CopyToAsyncUsesAsyncMethods()
        {
            var from = new MemoryStream();
            from.WriteByteArrayAsync("Hello".GetBytesUtf8(), CancellationToken);
            from.Position = 0;

            var noSyncIoStreamFrom = new NoSyncIoStream(from);
            await using (var sutFrom = WrapStream(noSyncIoStreamFrom))
            {
                var to = new MemoryStream();
                var noSyncIoStreamTo = new NoSyncIoStream(to);
                await using (var sutTo = WrapStream(noSyncIoStreamTo))
                {
                    await sutFrom.CopyToAsync(sutTo);
                }

                Encoding.UTF8.GetString(to.ToArray())
                    .Should()
                    .Be("Hello");

                noSyncIoStreamTo.SyncCalls.Should().BeEmpty();
            }

            noSyncIoStreamFrom.SyncCalls.Should().BeEmpty();
        }
    }
}