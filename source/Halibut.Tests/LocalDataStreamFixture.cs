using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Halibut.Tests
{
    public class LocalDataStreamFixture
    {
        [Fact]
        public async Task ShouldUseInMemoryReceiverLocallyToRead()
        {
            const string input = "Hello World!";
            var dataStream = DataStream.FromString(input);

            string result = null;
            await dataStream.Receiver().Read(async stream => result = await ReadStreamAsString(stream).ConfigureAwait(false)).ConfigureAwait(false);

            result.Should().Be(input);
        }

        [Fact]
        public void ShouldUseInMemoryReceiverLocallyToSaveToFile()
        {
            const string input = "We all live in a yellow submarine";
            var dataStream = DataStream.FromString(input);
            var filePath = Path.GetTempFileName();

            try
            {
                dataStream.Receiver().SaveTo(filePath);

                File.ReadAllText(filePath).Should().Be(input);
            }
            finally
            {
               File.Delete(filePath); 
            }
        }

        static Task<string> ReadStreamAsString(Stream stream)
        {
            using (var reader = new StreamReader(stream))
            {
                return Task.FromResult(reader.ReadToEnd());
            }
        }
    }
}
