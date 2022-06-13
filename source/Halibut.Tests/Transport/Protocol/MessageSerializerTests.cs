using System.IO;
using System.Text;
using Halibut.Transport;
using Halibut.Transport.Protocol;
using NUnit.Framework;

namespace Halibut.Tests.Transport.Protocol
{
    public class MessageSerializerTests
    {
        [Test]
        public void SendReceiveMessageShouldRoundTrip()
        {
            var sut = MessageSerializerBuilder.Build();
            using (var stream = new MemoryStream())
            {
                sut.WriteMessage(stream, "Test");
                stream.Position = 0;
                var result = sut.ReadMessage<string>(stream);
                Assert.AreEqual("Test", result);
            }
        }

        #if !NETFRAMEWORK
        [Test]
        public void SendReceiveMessageRewindableShouldRoundTrip()
        {
            using (var stream = new MemoryStream())
            using (var rewindableStream = new RewindableBufferStream(stream))
            {
                var sut = MessageSerializerBuilder.Build();
                sut.WriteMessage(stream, "Test");
                stream.Position = 0;
                Assert.AreEqual("Test", sut.ReadMessage<string>(rewindableStream));
            }
        }

        [Test]
        public void ReadMessageRewindableShouldNotConsumeTrailingData()
        {
            const string trailingData = "SomeOtherData";

            var sut = MessageSerializerBuilder.Build();
            using (var ms = new MemoryStream())
            using (var stream = new RewindableBufferStream(ms))
            {
                sut.WriteMessage(stream, "Test");
                var trailingBytes = Encoding.UTF8.GetBytes(trailingData);
                stream.Write(trailingBytes, 0, trailingBytes.Length);
                ms.Position = 0;

                using (var reader = new StreamReader(stream, new UTF8Encoding(false)))
                {
                    _ = sut.ReadMessage<string>(stream);
                    var trailingResult = reader.ReadToEnd();
                    Assert.AreEqual(trailingData, trailingResult);
                }
            }
        }
        #endif

        static class MessageSerializerBuilder
        {
            public static MessageSerializer Build() => new MessageSerializer();
        }
    }
}
