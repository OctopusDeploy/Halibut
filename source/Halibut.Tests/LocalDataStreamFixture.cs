using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class LocalDataStreamFixture : BaseTest
    {
        [Test]
        public async Task ShouldUseInMemoryReceiverLocallyToRead()
        {
            const string input = "Hello World!";
            var dataStream = DataStream.FromString(input);

            var result = string.Empty;
            await dataStream.Receiver().ReadAsync(async (stream, _) => result = await ReadStreamAsStringAsync(stream), CancellationToken);

            result.Should().Be(input);
        }

        [Test]
        public async Task ShouldUseInMemoryReceiverLocallyToSaveToFile()
        {
            const string input = "We all live in a yellow submarine";
            var dataStream = DataStream.FromString(input);
            var filePath = Path.GetTempFileName();
            try
            {
                await dataStream.Receiver().SaveToAsync(filePath, CancellationToken);

#if NET8_0_OR_GREATER
                (await File.ReadAllTextAsync(filePath)).Should().Be(input);
#else
                File.ReadAllText(filePath).Should().Be(input);
#endif
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
