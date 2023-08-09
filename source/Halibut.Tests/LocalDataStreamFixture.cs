using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Tests.Support.TestAttributes;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class LocalDataStreamFixture : BaseTest
    {
        [Test]
        [SyncAndAsync]
        public async Task ShouldUseInMemoryReceiverLocallyToRead(SyncOrAsync syncOrAsync)
        {
            const string input = "Hello World!";
            var dataStream = DataStream.FromString(input);

            string result = null;
            await syncOrAsync
                .WhenSync(() => dataStream.Receiver().Read(stream => result = ReadStreamAsString(stream)))
                .WhenAsync(async () => await dataStream.Receiver().ReadAsync(async (stream, _) => result = await ReadStreamAsStringAsync(stream), CancellationToken));

            result.Should().Be(input);
        }

        [Test]
        [SyncAndAsync]
        public async Task ShouldUseInMemoryReceiverLocallyToSaveToFile(SyncOrAsync syncOrAsync)
        {
            const string input = "We all live in a yellow submarine";
            var dataStream = DataStream.FromString(input);
            var filePath = Path.GetTempFileName();
            try
            {
                await syncOrAsync
                    .WhenSync(() => dataStream.Receiver().SaveTo(filePath))
                    .WhenAsync(async () => await dataStream.Receiver().SaveToAsync(filePath, CancellationToken));

                File.ReadAllText(filePath).Should().Be(input);
            }
            finally
            {
               File.Delete(filePath);
            }
        }
        

        static string ReadStreamAsString(Stream stream)
        {
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }
        
        static Task<string> ReadStreamAsStringAsync(Stream stream)
        {
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEndAsync();
            }
        }
    }
}
