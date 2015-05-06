using System.IO;
using NUnit.Framework;

namespace Halibut.Tests
{
    [TestFixture]
    public class LocalDataStreamFixture
    {
        [Test]
        public void ShouldUseInMemoryReceiverLocallyToRead()
        {
            const string input = "Hello World!";
            var dataStream = DataStream.FromString(input);

            string result = null;
            dataStream.Receiver().Read(stream => result = ReadStreamAsString(stream));

            Assert.AreEqual(input, result);
        }

        [Test]
        public void ShouldUseInMemoryReceiverLocallyToSaveToFile()
        {
            const string input = "We all live in a yellow submarine";
            var dataStream = DataStream.FromString(input);
            var filePath = Path.GetTempFileName();

            dataStream.Receiver().SaveTo(filePath);

            Assert.AreEqual(input, File.ReadAllText(filePath));
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
