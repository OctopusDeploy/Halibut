using System.IO;
using FluentAssertions;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class LocalDataStreamFixture
    {
        [Test]
        public void ShouldUseInMemoryReceiverLocallyToRead()
        {
            const string input = "Hello World!";
            var dataStream = DataStream.FromString(input);

            string result = null;
            dataStream.Receiver().Read(stream => result = ReadStreamAsString(stream));

            result.Should().Be(input);
        }

        [Test]
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

        private static string ReadStreamAsString(Stream stream)
        {
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
