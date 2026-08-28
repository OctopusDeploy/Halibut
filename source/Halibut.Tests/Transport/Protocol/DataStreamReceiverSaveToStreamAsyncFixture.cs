using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Queue.Redis.MessageStorage;
using Halibut.Tests.Support;
using Halibut.Transport.Protocol;
using NUnit.Framework;

namespace Halibut.Tests.Transport.Protocol
{
    public class DataStreamReceiverSaveToStreamAsyncFixture : BaseTest
    {
        static byte[] SomeBytes() => Encoding.UTF8.GetBytes("Hello from SaveToStreamAsync!");

        [Test]
        public async Task InMemoryDataStreamReceiver_SaveToStreamAsync_WritesTheWritersData()
        {
            var data = SomeBytes();
            var sut = new InMemoryDataStreamReceiver((stream, ct) => stream.WriteAsync(data, 0, data.Length, ct));

            using var destination = new MemoryStream();
            await sut.SaveToStreamAsync(destination, CancellationToken);

            destination.ToArray().Should().BeEquivalentTo(data);
        }

        [Test]
        public async Task TemporaryFileDataStreamReceiver_SaveToStreamAsync_WritesTheWritersData()
        {
            var data = SomeBytes();
            var sut = new TemporaryFileDataStreamReceiver((stream, ct) => stream.WriteAsync(data, 0, data.Length, ct));

            using var destination = new MemoryStream();
            await sut.SaveToStreamAsync(destination, CancellationToken);

            destination.ToArray().Should().BeEquivalentTo(data);
        }

        [Test]
        public async Task TemporaryFileStream_SaveToStreamAsync_WritesTheFilesDataAndDeletesTheSourceFile()
        {
            var data = SomeBytes();
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            await File.WriteAllBytesAsync(path, data, CancellationToken);

            var sut = new TemporaryFileStream(path, HalibutLog);

            using var destination = new MemoryStream();
            await sut.SaveToStreamAsync(destination, CancellationToken);

            destination.ToArray().Should().BeEquivalentTo(data);
            File.Exists(path).Should().BeFalse("the source temp file should be deleted once consumed");
        }

        [Test]
        public async Task TemporaryFileStream_SaveToStreamAsync_CannotBeCalledTwice()
        {
            var data = SomeBytes();
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            await File.WriteAllBytesAsync(path, data, CancellationToken);

            var sut = new TemporaryFileStream(path, HalibutLog);

            using (var destination = new MemoryStream())
            {
                await sut.SaveToStreamAsync(destination, CancellationToken);
            }

            using var secondDestination = new MemoryStream();
            await AssertException.Throws<InvalidOperationException>(async () => await sut.SaveToStreamAsync(secondDestination, CancellationToken));
        }

        [Test]
        public async Task DataStreamRehydrationDataDataStreamReceiver_SaveToStreamAsync_WritesTheSuppliedData()
        {
            var data = SomeBytes();
            var sourceStream = new MemoryStream(data);
            var sut = new DataStreamRehydrationDataDataStreamReceiver(() => new DataStreamRehydrationData(sourceStream));

            using var destination = new MemoryStream();
            await sut.SaveToStreamAsync(destination, CancellationToken);

            destination.ToArray().Should().BeEquivalentTo(data);
        }
    }
}
