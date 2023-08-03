using System;
using System.Collections;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Tests.Support;
using Halibut.Tests.Util;
using Halibut.Transport;
using Halibut.Transport.Protocol;
using NUnit.Framework;

namespace Halibut.Tests.Transport.Protocol
{
    public class MessageSerializerFixture : BaseTest
    {
        const long SmallLimit = 8L;
        const long LargeLimit = 16L * 1024L * 1024L;

        [Test]
        public void SendReceiveMessageShouldRoundTrip()
        {
            var sut = new MessageSerializerBuilder().Build();

            using (var stream = new MemoryStream())
            {
                sut.WriteMessage(stream, "Test");
                stream.Position = 0;
                using(var rewindableBufferStream  = new RewindableBufferStream(stream))
                {
                    var result = sut.ReadMessage<string>(rewindableBufferStream);
                    Assert.AreEqual("Test", result);
                }
            }
        }

        [Test]
        [TestCase(SmallLimit)]
        [TestCase(LargeLimit)]
        public async Task SendReceiveMessageAsyncShouldRoundTrip(long memoryLimitBytes)
        {
            var sut = new MessageSerializerBuilder()
                .WithAsyncMemoryLimits(memoryLimitBytes, memoryLimitBytes)
                .Build();

            using (var stream = new MemoryStream())
            {
                await sut.WriteMessageAsync(stream, "Some random test message", CancellationToken);
                stream.Position = 0;
                using (var rewindableBufferStream = new RewindableBufferStream(stream))
                {
                    var result = await sut.ReadMessageAsync<string>(rewindableBufferStream, CancellationToken);
                    Assert.AreEqual("Some random test message", result);
                }
            }
        }

        [Test]
        public void WriteMessage_ObservesThatMessageIsWritten()
        {
            var messageSerializerObserver = new TestMessageSerializerObserver();
            var sut = new MessageSerializerBuilder()
                .WithMessageSerializerObserver(messageSerializerObserver)
                .Build();

            using (var stream = new MemoryStream())
            {
                sut.WriteMessage(stream, "Repeating phrase that compresses. Repeating phrase that compresses. Repeating phrase that compresses.");
            }

            var writtenMessage = messageSerializerObserver.MessagesWritten.Should().ContainSingle().Subject;
            writtenMessage.CompressedBytesWritten.Should().Be(55);
            writtenMessage.CompressedBytesWrittenIntoMemory.Should().Be(0);
            messageSerializerObserver.MessagesRead.Should().BeEmpty();
        }

        [Test]
        [TestCase(SmallLimit, 0)]
        [TestCase(LargeLimit, 55)]
        public async Task WriteMessageAsync_ObservesThatMessageIsWritten(long writeIntoMemoryLimitBytes, long expectedCompressedBytesWrittenIntoMemory)
        {
            var messageSerializerObserver = new TestMessageSerializerObserver();
            var sut = new MessageSerializerBuilder()
                .WithAsyncMemoryLimits(0, writeIntoMemoryLimitBytes)
                .WithMessageSerializerObserver(messageSerializerObserver)
                .Build();

            using (var stream = new MemoryStream())
            {
                await sut.WriteMessageAsync(stream, "Repeating phrase that compresses. Repeating phrase that compresses. Repeating phrase that compresses.", CancellationToken);
            }

            var writtenMessage = messageSerializerObserver.MessagesWritten.Should().ContainSingle().Subject;
            writtenMessage.CompressedBytesWritten.Should().Be(55);
            writtenMessage.CompressedBytesWrittenIntoMemory.Should().Be(expectedCompressedBytesWrittenIntoMemory);
            messageSerializerObserver.MessagesRead.Should().BeEmpty();
        }

        [Test]
        public void ReadMessage_ObservesThatMessageIsRead()
        {
            var messageSerializerObserver = new TestMessageSerializerObserver();
            var sut = new MessageSerializerBuilder()
                .WithMessageSerializerObserver(messageSerializerObserver)
                .Build();

            using (var stream = new MemoryStream())
            {
                var writingSerializer = new MessageSerializerBuilder().Build();
                writingSerializer.WriteMessage(stream, "Repeating phrase that compresses. Repeating phrase that compresses. Repeating phrase that compresses.");

                stream.Position = 0;
                using (var rewindableStream = new RewindableBufferStream(stream))
                {
                    sut.ReadMessage<string>(rewindableStream);
                }
            }

            var readMessage = messageSerializerObserver.MessagesRead.Should().ContainSingle().Subject;
            readMessage.CompressedBytesRead.Should().Be(55);
            readMessage.DecompressedBytesRead.Should().Be(120);
            readMessage.DecompressedBytesReadIntoMemory.Should().Be(0);
            messageSerializerObserver.MessagesWritten.Should().BeEmpty();
        }

        [Test]
        [TestCase(SmallLimit, SmallLimit)]
        [TestCase(LargeLimit, 120)]
        public async Task ReadMessageAsync_ObservesThatMessageIsRead(long readIntoMemoryLimitBytes, long expectedDecompressedBytesReadIntoMemory)
        {
            var messageSerializerObserver = new TestMessageSerializerObserver();
            var sut = new MessageSerializerBuilder()
                .WithAsyncMemoryLimits(readIntoMemoryLimitBytes, 0)
                .WithMessageSerializerObserver(messageSerializerObserver)
                .Build();

            using (var stream = new MemoryStream())
            {
                var writingSerializer = new MessageSerializerBuilder().Build();
                writingSerializer.WriteMessage(stream, "Repeating phrase that compresses. Repeating phrase that compresses. Repeating phrase that compresses.");

                stream.Position = 0;
                using (var rewindableStream = new RewindableBufferStream(stream))
                {
                    await sut.ReadMessageAsync<string>(rewindableStream, CancellationToken);
                }
            }

            var readMessage = messageSerializerObserver.MessagesRead.Should().ContainSingle().Subject;
            readMessage.CompressedBytesRead.Should().Be(55);
            readMessage.DecompressedBytesRead.Should().Be(120);
            readMessage.DecompressedBytesReadIntoMemory.Should().Be(expectedDecompressedBytesReadIntoMemory);
            messageSerializerObserver.MessagesWritten.Should().BeEmpty();
        }

        [Test]
        public void BackwardsCompatibility_ExtraParametersInServerErrorAreIgnored()
        {
            var sut = new MessageSerializerBuilder().Build();
            // This is bson of a RequestMessage which contains a hacked ServerError which has an extra field which no version of Halibut will ever have.
            // The test expects that the serializer will ignore the extra field. 
            // This is to show that as extra fields can be added to the ServerError and they will be ignored.
            var base64Bson = "nY6xTgNBDESHDTR8xRakQ1dRICSaRIfSBCHIDyxZk1tpsz6tfQE+mP/AgQVRU7gYe96Mn06A2ZpEwo7Qm3AX+j4SrgCsQk7Pk3abGoqMXLV7qKy85dw9ki2KUCMvffPCpYgzYyVKxIxq5YoP027fOk5NvDDDRdKQsuD2T1P/tqVRkyV3a9KB4z3rHU8lNsMyJyr667rxX0nt2B/LNsfnr/8fCbfIYfgZzC3JAC+8NziVnR++Mf+acvZ0oGqbAwHnlWTKav5P";
            using (var stream = new RewindableBufferStream(new MemoryStream(Convert.FromBase64String(base64Bson))))
            {
                var result = sut.ReadMessage<ResponseMessage>(stream);
                result.Error.Should().NotBeNull();
                result.Error.Message = "foo";
                result.Error.HalibutErrorType = "MethodNotFoundHalibutClientException";
            }
        }

        [Test]
        public void WhenTheStreamEndsBeforeAnyBytesAreRead_AnEndOfStreamExceptionIsThrown()
        {
            var sut = new MessageSerializerBuilder().Build();
            using (var stream = new RewindableBufferStream(new MemoryStream(new byte[0])))
            {
                Assert.Throws<EndOfStreamException>(() => sut.ReadMessage<ResponseMessage>(stream));
            }
        }
        
        [Test]
        public void WhenTheStreamEndsMidWayThroughReadingAMessage_AEndOfStreamExceptionIsThrown()
        {
            var sut = new MessageSerializerBuilder().Build();
            var completeBytes = CreateBytesFromMessage("Hello this is the message");
            var someOfTheBytes = completeBytes.SubArray(0, completeBytes.Length - 5);
            using (var stream = new RewindableBufferStream(new MemoryStream(someOfTheBytes)))
            {
                Assert.Throws<EndOfStreamException>(() => sut.ReadMessage<ResponseMessage>(stream));
            }
        }
        
        [Test]
        public void WhenTheStreamContainsAnIncompleteZipStream_SomeSortOfZipErrorIsThrown()
        {
            var sut = new MessageSerializerBuilder().Build();
            var completeBytes = CreateBytesFromMessage(Some.RandomAsciiStringOfLength(22000));
            var badZipStream = new byte[64000];
            Array.Copy(completeBytes, 0, badZipStream, 0, 30);
            
            using (var stream = new RewindableBufferStream(new MemoryStream(badZipStream)))
            {
                Assert.Throws<InvalidDataException>(() => sut.ReadMessage<ResponseMessage>(stream));
            }
        }
        
        [Test]
        public void WhenTheStreamContainsAnInvalidObject_SomeSortOfJsonErrorsThrown()
        {
            var sut = new MessageSerializerBuilder().Build();
            
            var deflatedString = DeflateString("Some invalid json/bson");
            using (var stream = new RewindableBufferStream(new MemoryStream(deflatedString)))
            {
                Assert.Throws<Newtonsoft.Json.JsonSerializationException>(() => sut.ReadMessage<ResponseMessage>(stream));
            }
        }
        
        [Test]
        public void SendReceiveMessageRewindableShouldRoundTrip()
        {
            using (var stream = new MemoryStream())
            using (var rewindableStream = new RewindableBufferStream(stream))
            {
                var sut = new MessageSerializerBuilder().Build();
                sut.WriteMessage(stream, "Test");
                stream.Position = 0;
                Assert.AreEqual("Test", sut.ReadMessage<string>(rewindableStream));
            }
        }

        [Test]
        public void ReadMessage_Rewindable_ObservesThatMessageIsRead()
        {
            var messageSerializerObserver = new TestMessageSerializerObserver();
            var sut = new MessageSerializerBuilder()
                .WithMessageSerializerObserver(messageSerializerObserver)
                .Build();

            using (var stream = new MemoryStream())
            using (var rewindableStream = new RewindableBufferStream(stream))
            {
                var writingSerializer = new MessageSerializerBuilder().Build();
                writingSerializer.WriteMessage(stream, "Repeating phrase that compresses. Repeating phrase that compresses. Repeating phrase that compresses.");

                stream.Position = 0;
                sut.ReadMessage<string>(rewindableStream);
            }

            var readMessage = messageSerializerObserver.MessagesRead.Should().ContainSingle().Subject;
            readMessage.CompressedBytesRead.Should().Be(55);
            readMessage.DecompressedBytesRead.Should().Be(120);
            readMessage.DecompressedBytesReadIntoMemory.Should().Be(0);
            messageSerializerObserver.MessagesWritten.Should().BeEmpty();
        }

        [Test]
        public void ReadMessageRewindableShouldNotConsumeTrailingData()
        {
            const string trailingData = "SomeOtherData";

            var sut = new MessageSerializerBuilder().Build();
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

        [Test]
        public void ReadMessage_Rewindable_ShouldNotObserveTrailingDataWhenReading()
        {
            const string trailingData = "SomeOtherData";

            var messageSerializerObserver = new TestMessageSerializerObserver();
            var sut = new MessageSerializerBuilder()
                .WithMessageSerializerObserver(messageSerializerObserver)
                .Build();

            using (var stream = new MemoryStream())
            using (var rewindableStream = new RewindableBufferStream(stream))
            {
                var writingSerializer = new MessageSerializerBuilder().Build();
                writingSerializer.WriteMessage(stream, "Repeating phrase that compresses. Repeating phrase that compresses. Repeating phrase that compresses.");

                var trailingBytes = Encoding.UTF8.GetBytes(trailingData);
                stream.Write(trailingBytes, 0, trailingBytes.Length);

                stream.Position = 0;
                sut.ReadMessage<string>(rewindableStream);
            }

            var readMessage = messageSerializerObserver.MessagesRead.Should().ContainSingle().Subject;
            readMessage.CompressedBytesRead.Should().Be(55);
            readMessage.DecompressedBytesRead.Should().Be(120);
            readMessage.DecompressedBytesReadIntoMemory.Should().Be(0);
        }
        
        static byte[] CreateBytesFromMessage(object message)
        {
            var writingSerializer = new MessageSerializerBuilder().Build();

            using (var stream = new MemoryStream())
            {
                writingSerializer.WriteMessage(stream, message);
                stream.Position = 0;
                return stream.ToArray();
            }
        }

        static byte[] DeflateString(string s)
        {
            using (var memoryStream = new MemoryStream())
            {
                using (var zip = new DeflateStream(memoryStream, CompressionMode.Compress, true))
                {
                    zip.WriteString(s);
                    zip.Flush();
                }
                memoryStream.Position = 0;
                return memoryStream.ToArray();
            }
        }
    }
}
