using System;
using System.Collections;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Tests.Support;
using Halibut.Transport;
using Halibut.Transport.Protocol;
using NUnit.Framework;

namespace Halibut.Tests.Transport.Protocol
{
    public class MemoryLimitTestCaseSource : IEnumerable
    {
        const long SmallMemoryLimit = 16L;
        const long LargeMemoryLimit = 16L * 1024L * 1024L;

        public IEnumerator GetEnumerator()
        {
            yield return SmallMemoryLimit;
            yield return LargeMemoryLimit;
        }
    }

    public class MessageSerializerFixture
    {
        [Test]
        public void SendReceiveMessageShouldRoundTrip()
        {
            var sut = new MessageSerializerBuilder().Build();

            using (var stream = new MemoryStream())
            {
                sut.WriteMessage(stream, "Test");
                stream.Position = 0;
                var result = sut.ReadMessage<string>(stream);
                Assert.AreEqual("Test", result);
            }
        }

        [Test]
        [Ignore("Test for local testing purposes")]
        public async Task GeneralTimingScratchPad_TodoRemove()
        {
            var sut = new MessageSerializerBuilder().Build();

            //{
            //    var m = File.ReadAllText(@"C:\Users\Stephen Burman\Downloads\inflated-file.json");

            //    var t = new ResponseMessage()
            //    {
            //        Id = m,
            //        Result = m,
            //        Error = new ServerError()
            //        {
            //            Details = m,
            //            HalibutErrorType = m,
            //            Message = m
            //        }
            //    };

            //    using (var stream = File.OpenWrite(@"C:\Users\Stephen Burman\Downloads\inflated2.zip"))
            //    {
            //        await sut.WriteMessageAsync(stream, t, CancellationToken.None);
            //        //sut.WriteMessage(stream, t);
            //    }
            //}


            //using (var stream = File.OpenRead(@"C:\Users\Stephen Burman\Downloads\inflated.zip"))
            ////using (var stream = new MemoryStream())
            //{
            //    //await sut.WriteMessageAsync(stream, m, CancellationToken.None);
            //    //stream.Position = 0;
            //    var result = await sut.ReadMessageAsync<string>(stream, CancellationToken.None);
            //    //Assert.AreEqual(m, result);
            //}


            using (var stream = File.OpenRead(@"C:\Users\Stephen Burman\Downloads\inflated2.zip"))
            //using (var stream = new MemoryStream())
            {
                //await sut.WriteMessageAsync(stream, m, CancellationToken.None);
                //stream.Position = 0;
                var r = await sut.ReadMessageAsync<ResponseMessage>(stream, CancellationToken.None);

                //var r = sut.ReadMessage<ResponseMessage>(stream);

                //Assert.AreEqual(m, result);
            }

            ////var time = stopwatch.Elapsed;
            await Task.Delay(5000);
        }

        [Test]
        [TestCaseSource(typeof(MemoryLimitTestCaseSource))]
        public async Task SendReceiveMessageAsyncShouldRoundTrip(long readIntoMemoryLimitBytes)
        {
            var sut = new MessageSerializerBuilder()
                .WithReadIntoMemoryLimitBytes(readIntoMemoryLimitBytes)
                .Build();

            using (var stream = new MemoryStream())
            {
                await sut.WriteMessageAsync(stream, "Some random test message", CancellationToken.None);
                
                stream.Position = 0;
                var result = await sut.ReadMessageAsync<string>(stream, CancellationToken.None);
                
                Assert.AreEqual("Some random test message", result);
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

            var compressedBytesWritten = messageSerializerObserver.MessagesWritten.Should().ContainSingle().Subject;
            compressedBytesWritten.Should().Be(55);
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
                sut.ReadMessage<string>(stream);
            }

            var readMessage = messageSerializerObserver.MessagesRead.Should().ContainSingle().Subject;
            readMessage.CompressedBytesRead.Should().Be(55);
            readMessage.DecompressedBytesRead.Should().Be(120);
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
            using (var stream = new MemoryStream(Convert.FromBase64String(base64Bson)))
            {
                var result = sut.ReadMessage<ResponseMessage>(stream);
                result.Error.Should().NotBeNull();
                result.Error.Message = "foo";
                result.Error.HalibutErrorType = "MethodNotFoundHalibutClientException";
            }
        }

        #if !NETFRAMEWORK
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
        [TestCaseSource(typeof(MemoryLimitTestCaseSource))]
        public async Task SendReceiveMessageAsyncRewindableShouldRoundTrip(long readIntoMemoryLimitBytes)
        {
            using (var stream = new MemoryStream())
            using (var rewindableStream = new RewindableBufferStream(stream))
            {
                var sut = new MessageSerializerBuilder()
                    .WithReadIntoMemoryLimitBytes(readIntoMemoryLimitBytes)
                    .Build();

                await sut.WriteMessageAsync(stream, "Test", CancellationToken.None);

                stream.Position = 0;
                var result = await sut.ReadMessageAsync<string>(rewindableStream, CancellationToken.None);

                Assert.AreEqual("Test", result);
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
        [TestCaseSource(typeof(MemoryLimitTestCaseSource))]
        public async Task ReadMessageAsyncRewindableShouldNotConsumeTrailingData(long readIntoMemoryLimitBytes)
        {
            const string trailingData = "SomeOtherData";

            var sut = new MessageSerializerBuilder()
                .WithReadIntoMemoryLimitBytes(readIntoMemoryLimitBytes)
                .Build();

            using (var ms = new MemoryStream())
            using (var stream = new RewindableBufferStream(ms))
            {
                await sut.WriteMessageAsync(stream, "Test", CancellationToken.None);
                var trailingBytes = Encoding.UTF8.GetBytes(trailingData);
                stream.Write(trailingBytes, 0, trailingBytes.Length);
                ms.Position = 0;

                using (var reader = new StreamReader(stream, new UTF8Encoding(false)))
                {
                    _ = await sut.ReadMessageAsync<string>(stream, CancellationToken.None);
                    var trailingResult = await reader.ReadToEndAsync();
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
        }
#endif
    }
}
