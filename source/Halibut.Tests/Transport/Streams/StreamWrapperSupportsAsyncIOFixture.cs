using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Tests.Support.Streams;
using Halibut.Tests.Util;
using Halibut.Transport.Streams;
using NUnit.Framework;

namespace Halibut.Tests.Transport.Streams
{
    public abstract class StreamWrapperSupportsAsyncIOFixture : BaseTest
    {
        protected abstract AsyncStream WrapStream(Stream stream);

        [Test]
        public async Task ApmBeginReadUsesAsyncMethods()
        {
            var memoryStream = new MemoryStream();
            memoryStream.WriteString("Hello");
            memoryStream.Position = 0;

            var syncIoRecordingStream = new SyncIoRecordingStream(memoryStream);

            await using (var sut = WrapStream(syncIoRecordingStream))
            {
                var buffer = new byte[100];
                var read = await Task<int>.Factory.FromAsync(sut.BeginRead, sut.EndRead, buffer, 0, buffer.Length, null);

                read.Should().Be(5);

                Encoding.UTF8.GetString(buffer, 0, 5)
                    .Should()
                    .Be("Hello");
            }

            syncIoRecordingStream.SyncCalls.Should().BeEmpty();
        }

        [Test]
        public async Task ApmBeginWriteUsesAsyncMethods()
        {
            var memoryStream = new MemoryStream();

            var syncIoRecordingStream = new SyncIoRecordingStream(memoryStream);

            await using (var sut = WrapStream(syncIoRecordingStream))
            {
                var buffer = "Hello".GetBytesUtf8();
                await Task.Factory.FromAsync(sut.BeginWrite, sut.EndWrite, buffer, 0, buffer.Length, null);

                await sut.FlushAsync(CancellationToken);

                memoryStream.Position = 0;

                Encoding.UTF8.GetString(memoryStream.ToArray())
                    .Should()
                    .Be("Hello");
            }

            syncIoRecordingStream.SyncCalls.Should().BeEmpty();
        }

        [Test]
        public async Task FlushAsyncUsesFlushAsync()
        {
            var memoryStream = new MemoryStream();

            var syncIoRecordingStream = new SyncIoRecordingStream(memoryStream);

            await using (var sut = WrapStream(syncIoRecordingStream))
            {
                await sut.WriteByteArrayAsync("Hello".GetBytesUtf8(), CancellationToken);
                await sut.FlushAsync(CancellationToken);

                memoryStream.Position = 0;

                Encoding.UTF8.GetString(memoryStream.ToArray())
                    .Should()
                    .Be("Hello");
            }

            syncIoRecordingStream.SyncCalls.Should().BeEmpty();
        }

        [Test]
        public async Task CopyToAsyncUsesAsyncMethods()
        {
            var from = new MemoryStream();
            from.WriteByteArrayAsync("Hello".GetBytesUtf8(), CancellationToken);
            from.Position = 0;

            var syncIoRecordingStreamFrom = new SyncIoRecordingStream(from);
            await using (var sutFrom = WrapStream(syncIoRecordingStreamFrom))
            {
                var to = new MemoryStream();
                var syncIoRecordingStreamTo = new SyncIoRecordingStream(to);
                await using (var sutTo = WrapStream(syncIoRecordingStreamTo))
                {
                    await sutFrom.CopyToAsync(sutTo);
                }

                Encoding.UTF8.GetString(to.ToArray())
                    .Should()
                    .Be("Hello");

                syncIoRecordingStreamTo.SyncCalls.Should().BeEmpty();
            }

            syncIoRecordingStreamFrom.SyncCalls.Should().BeEmpty();
        }
    }
}