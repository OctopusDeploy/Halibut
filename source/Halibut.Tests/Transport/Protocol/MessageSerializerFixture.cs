using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Diagnostics;
using Halibut.Tests.Support;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Util;
using Halibut.Transport;
using Halibut.Transport.Protocol;
using NUnit.Framework;

namespace Halibut.Tests.Transport.Protocol
{
    public class MessageSerializerFixture : BaseTest
    {
        [Test]
        [TestCaseSource(typeof(MessageSerializerTestCaseSource))]
        public async Task SendReceiveMessageShouldRoundTrip(MessageSerializerTestCase testCase)
        {
            var sut = new MessageSerializerBuilder(new LogFactory())
                .WithAsyncMemoryLimits(testCase.AsyncMemoryLimit, testCase.AsyncMemoryLimit)
                .Build();

            using (var stream = new MemoryStream())
            {
                await WriteMessage(testCase, sut, stream, "Some random test message");
                stream.Position = 0;
                using (var rewindableBufferStream = new RewindableBufferStream(stream))
                {
                    var result = await ReadMessage<string>(testCase, sut, rewindableBufferStream);
                    Assert.AreEqual("Some random test message", result);
                }
            }
        }
        
        [Test]
        [TestCaseSource(typeof(MessageSerializerTestCaseSource))]
        public async Task WriteMessage_ObservesThatMessageIsWritten(MessageSerializerTestCase testCase)
        {
            var messageSerializerObserver = new TestMessageSerializerObserver();
            var sut = new MessageSerializerBuilder(new LogFactory())
                .WithAsyncMemoryLimits(testCase.AsyncMemoryLimit, testCase.AsyncMemoryLimit)
                .WithMessageSerializerObserver(messageSerializerObserver)
                .Build();

            using (var stream = new MemoryStream())
            {
                await WriteMessage(testCase, sut, stream, "Repeating phrase that compresses. Repeating phrase that compresses. Repeating phrase that compresses.");
            }

            var writtenMessage = messageSerializerObserver.MessagesWritten.Should().ContainSingle().Subject;
            writtenMessage.CompressedBytesWritten.Should().Be(55);
            var expectedCompressedBytesWrittenIntoMemory = testCase.AsyncMemoryLimit > 55 ? 55 : 0;
            writtenMessage.CompressedBytesWrittenIntoMemory.Should().Be(expectedCompressedBytesWrittenIntoMemory);
            messageSerializerObserver.MessagesRead.Should().BeEmpty();
        }
        
        [Test]
        [TestCaseSource(typeof(MessageSerializerTestCaseSource))]
        public async Task ReadMessage_ObservesThatMessageIsRead(MessageSerializerTestCase testCase)
        {
            var messageSerializerObserver = new TestMessageSerializerObserver();
            var sut = new MessageSerializerBuilder(new LogFactory())
                .WithAsyncMemoryLimits(testCase.AsyncMemoryLimit, testCase.AsyncMemoryLimit)
                .WithMessageSerializerObserver(messageSerializerObserver)
                .Build();

            using (var stream = new MemoryStream())
            {
                var writingSerializer = new MessageSerializerBuilder(new LogFactory())
                    .WithAsyncMemoryLimits(testCase.AsyncMemoryLimit, testCase.AsyncMemoryLimit)
                    .Build();
                await WriteMessage(testCase, writingSerializer, stream, "Repeating phrase that compresses. Repeating phrase that compresses. Repeating phrase that compresses.");

                stream.Position = 0;
                using (var rewindableStream = new RewindableBufferStream(stream))
                {
                    await ReadMessage<string>(testCase, sut, rewindableStream);
                }
            }

            var readMessage = messageSerializerObserver.MessagesRead.Should().ContainSingle().Subject;
            readMessage.CompressedBytesRead.Should().Be(55);
            readMessage.DecompressedBytesRead.Should().Be(120);
            var expectedDecompressedBytesReadIntoMemory = Math.Min(testCase.AsyncMemoryLimit, 120);
            readMessage.DecompressedBytesReadIntoMemory.Should().Be(expectedDecompressedBytesReadIntoMemory);
            messageSerializerObserver.MessagesWritten.Should().BeEmpty();
        }
        
        [Test]
        [TestCaseSource(typeof(MessageSerializerTestCaseSource))]
        public async Task BackwardsCompatibility_ExtraParametersInServerErrorAreIgnored(MessageSerializerTestCase testCase)
        {
            var sut = new MessageSerializerBuilder(new LogFactory())
                .WithAsyncMemoryLimits(testCase.AsyncMemoryLimit, testCase.AsyncMemoryLimit)
                .Build();
            // This is bson of a RequestMessage which contains a hacked ServerError which has an extra field which no version of Halibut will ever have.
            // The test expects that the serializer will ignore the extra field. 
            // This is to show that as extra fields can be added to the ServerError and they will be ignored.
            var base64Bson = "nY6xTgNBDESHDTR8xRakQ1dRICSaRIfSBCHIDyxZk1tpsz6tfQE+mP/AgQVRU7gYe96Mn06A2ZpEwo7Qm3AX+j4SrgCsQk7Pk3abGoqMXLV7qKy85dw9ki2KUCMvffPCpYgzYyVKxIxq5YoP027fOk5NvDDDRdKQsuD2T1P/tqVRkyV3a9KB4z3rHU8lNsMyJyr667rxX0nt2B/LNsfnr/8fCbfIYfgZzC3JAC+8NziVnR++Mf+acvZ0oGqbAwHnlWTKav5P";
            using (var stream = new RewindableBufferStream(new MemoryStream(Convert.FromBase64String(base64Bson))))
            {
                var result = await ReadMessage<ResponseMessage>(testCase, sut, stream);
                result.Error.Should().NotBeNull();
                result.Error.Message = "foo";
                result.Error.HalibutErrorType = "MethodNotFoundHalibutClientException";
            }
        }
        
        [Test]
        [TestCaseSource(typeof(MessageSerializerTestCaseSource))]
        public async Task WhenTheStreamEndsBeforeAnyBytesAreRead_AnEndOfStreamExceptionIsThrown(MessageSerializerTestCase testCase)
        {
            var sut = new MessageSerializerBuilder(new LogFactory())
                .WithAsyncMemoryLimits(testCase.AsyncMemoryLimit, testCase.AsyncMemoryLimit)
                .Build();
            using (var stream = new RewindableBufferStream(new MemoryStream(new byte[0])))
            {
                await AssertionExtensions.Should(() => ReadMessage<ResponseMessage>(testCase, sut, stream)).ThrowAsync<EndOfStreamException>();
            }
        }
        
        [Test]
        [TestCaseSource(typeof(MessageSerializerTestCaseSource))]
        public async Task WhenTheStreamEndsMidWayThroughReadingAMessage_AEndOfStreamExceptionIsThrown(MessageSerializerTestCase testCase)
        {
            var sut = new MessageSerializerBuilder(new LogFactory())
                .WithAsyncMemoryLimits(testCase.AsyncMemoryLimit, testCase.AsyncMemoryLimit)
                .Build();
            var completeBytes = CreateBytesFromMessage("Hello this is the message");
            var someOfTheBytes = completeBytes.SubArray(0, completeBytes.Length - 5);
            using (var stream = new RewindableBufferStream(new MemoryStream(someOfTheBytes)))
            {
                await AssertionExtensions.Should(() => ReadMessage<ResponseMessage>(testCase, sut, stream)).ThrowAsync<EndOfStreamException>();
            }
        }
        
        [Test]
        [TestCaseSource(typeof(MessageSerializerTestCaseSource))]
        public async Task WhenTheStreamContainsAnIncompleteZipStream_SomeSortOfZipErrorIsThrown(MessageSerializerTestCase testCase)
        {
            var sut = new MessageSerializerBuilder(new LogFactory())
                .WithAsyncMemoryLimits(testCase.AsyncMemoryLimit, testCase.AsyncMemoryLimit)
                .Build();
            var completeBytes = CreateBytesFromMessage(Some.RandomAsciiStringOfLength(22000));
            var badZipStream = new byte[64000];
            Array.Copy(completeBytes, 0, badZipStream, 0, 30);

            using (var stream = new RewindableBufferStream(new MemoryStream(badZipStream)))
            {
                await AssertionExtensions.Should(() => ReadMessage<ResponseMessage>(testCase, sut, stream)).ThrowAsync<InvalidDataException>();
            }
        }

        [Test]
        [TestCaseSource(typeof(MessageSerializerTestCaseSource))]
        public async Task WhenTheStreamContainsAnInvalidObject_SomeSortOfJsonErrorsThrown(MessageSerializerTestCase testCase)
        {
            var sut = new MessageSerializerBuilder(new LogFactory())
                .WithAsyncMemoryLimits(testCase.AsyncMemoryLimit, testCase.AsyncMemoryLimit)
                .Build();

            var deflatedString = DeflateString("Some invalid json/bson");
            using (var stream = new RewindableBufferStream(new MemoryStream(deflatedString)))
            {
                await AssertionExtensions.Should(() => ReadMessage<ResponseMessage>(testCase, sut, stream)).ThrowAsync<Newtonsoft.Json.JsonSerializationException>();
            }
        }
        
        [Test]
        [TestCaseSource(typeof(MessageSerializerTestCaseSource))]
        public async Task SendReceiveMessageRewindableShouldRoundTrip(MessageSerializerTestCase testCase)
        {
            using (var stream = new MemoryStream())
            using (var rewindableStream = new RewindableBufferStream(stream))
            {
                var sut = new MessageSerializerBuilder(new LogFactory())
                    .WithAsyncMemoryLimits(testCase.AsyncMemoryLimit, testCase.AsyncMemoryLimit)
                    .Build();
                await WriteMessage(testCase, sut, stream, "Test");
                stream.Position = 0;
                Assert.AreEqual("Test", await ReadMessage<string>(testCase, sut, rewindableStream));
            }
        }
        
        [Test]
        [TestCaseSource(typeof(MessageSerializerTestCaseSource))]
        public async Task ReadMessage_Rewindable_ObservesThatMessageIsRead(MessageSerializerTestCase testCase)
        {
            var messageSerializerObserver = new TestMessageSerializerObserver();
            var sut = new MessageSerializerBuilder(new LogFactory())
                .WithAsyncMemoryLimits(testCase.AsyncMemoryLimit, testCase.AsyncMemoryLimit)
                .WithMessageSerializerObserver(messageSerializerObserver)
                .Build();

            using (var stream = new MemoryStream())
            using (var rewindableStream = new RewindableBufferStream(stream))
            {
                var writingSerializer = new MessageSerializerBuilder(new LogFactory())
                    .WithAsyncMemoryLimits(testCase.AsyncMemoryLimit, testCase.AsyncMemoryLimit)
                    .Build();
                await WriteMessage(testCase, writingSerializer, stream, "Repeating phrase that compresses. Repeating phrase that compresses. Repeating phrase that compresses.");

                stream.Position = 0;
                await ReadMessage<string>(testCase, sut, rewindableStream);
            }

            var readMessage = messageSerializerObserver.MessagesRead.Should().ContainSingle().Subject;
            readMessage.CompressedBytesRead.Should().Be(55);
            readMessage.DecompressedBytesRead.Should().Be(120);
            var expectedDecompressedBytesReadIntoMemory = Math.Min(testCase.AsyncMemoryLimit, 120);
            readMessage.DecompressedBytesReadIntoMemory.Should().Be(expectedDecompressedBytesReadIntoMemory);
            messageSerializerObserver.MessagesWritten.Should().BeEmpty();
        }

        [Test]
        [TestCaseSource(typeof(MessageSerializerTestCaseSource))]
        public async Task ReadMessageRewindableShouldNotConsumeTrailingData(MessageSerializerTestCase testCase)
        {
            const string trailingData = "SomeOtherData";

            var sut = new MessageSerializerBuilder(new LogFactory())
                .WithAsyncMemoryLimits(testCase.AsyncMemoryLimit, testCase.AsyncMemoryLimit)
                .Build();

            using (var ms = new MemoryStream())
            using (var stream = new RewindableBufferStream(ms))
            {
                await WriteMessage(testCase, sut, stream, "Test");
                var trailingBytes = Encoding.UTF8.GetBytes(trailingData);
                stream.Write(trailingBytes, 0, trailingBytes.Length);
                ms.Position = 0;

                using (var reader = new StreamReader(stream, new UTF8Encoding(false)))
                {
                    _ = await ReadMessage<string>(testCase, sut, stream);
                    var trailingResult = reader.ReadToEnd();
                    Assert.AreEqual(trailingData, trailingResult);
                }
            }
        }

        [Test]
        [TestCaseSource(typeof(MessageSerializerTestCaseSource))]
        public async Task ReadMessage_Rewindable_ShouldNotObserveTrailingDataWhenReading(MessageSerializerTestCase testCase)
        {
            const string trailingData = "SomeOtherData";

            var messageSerializerObserver = new TestMessageSerializerObserver();
            var sut = new MessageSerializerBuilder(new LogFactory())
                .WithAsyncMemoryLimits(testCase.AsyncMemoryLimit, testCase.AsyncMemoryLimit)
                .WithMessageSerializerObserver(messageSerializerObserver)
                .Build();

            using (var stream = new MemoryStream())
            using (var rewindableStream = new RewindableBufferStream(stream))
            {
                var writingSerializer = new MessageSerializerBuilder(new LogFactory())
                    .WithAsyncMemoryLimits(testCase.AsyncMemoryLimit, testCase.AsyncMemoryLimit)
                    .Build();
                await WriteMessage(testCase, writingSerializer, stream, "Repeating phrase that compresses. Repeating phrase that compresses. Repeating phrase that compresses.");

                var trailingBytes = Encoding.UTF8.GetBytes(trailingData);
                stream.Write(trailingBytes, 0, trailingBytes.Length);

                stream.Position = 0;
                await ReadMessage<string>(testCase, sut, rewindableStream);
            }

            var readMessage = messageSerializerObserver.MessagesRead.Should().ContainSingle().Subject;
            readMessage.CompressedBytesRead.Should().Be(55);
            readMessage.DecompressedBytesRead.Should().Be(120);
            var expectedDecompressedBytesReadIntoMemory = Math.Min(testCase.AsyncMemoryLimit, 120);
            readMessage.DecompressedBytesReadIntoMemory.Should().Be(expectedDecompressedBytesReadIntoMemory);
        }
        
        static byte[] CreateBytesFromMessage(object message)
        {
            var writingSerializer = new MessageSerializerBuilder(new LogFactory()).Build();

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
        
        async Task<T> ReadMessage<T>(MessageSerializerTestCase testCase, MessageSerializer messageSerializer, RewindableBufferStream rewindableBufferStream)
        {
            if (testCase.SyncOrAsync == SyncOrAsync.Async)
            {
                return await messageSerializer.ReadMessageAsync<T>(rewindableBufferStream, CancellationToken);
            }

            return messageSerializer.ReadMessage<T>(rewindableBufferStream);
        }

        async Task WriteMessage(MessageSerializerTestCase testCase, MessageSerializer messageSerializer, Stream stream, string message)
        {
            if (testCase.SyncOrAsync == SyncOrAsync.Async)
            {
                await messageSerializer.WriteMessageAsync(stream, message, CancellationToken);
                return;
            }

            messageSerializer.WriteMessage(stream, message);
        }
    }
}
