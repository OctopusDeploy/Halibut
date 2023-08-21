using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Tests.Support;
using Halibut.Tests.Util;
using Halibut.Transport.Streams;
using NUnit.Framework;

namespace Halibut.Tests.Transport.Streams
{
    public class ErrorRecordingStreamFixture
    {
        [Test]
        public void WhenAskedToCloseTheInnerStreamItIsClosed()
        {
            using var tmpDir = new TemporaryDirectory();
            var path = tmpDir.RandomFileName();
            using (FileStream fs = File.OpenWrite(path))
            {
                using (var errorRecordingStream = new ErrorRecordingStream(fs, closeInner: true))
                {
                    errorRecordingStream.WriteString("Hello");
                }

                Assert.Throws<ObjectDisposedException>(() => fs.WriteString("not this"));
            }

            File.ReadAllText(path).Should().Be("Hello");
        }
        
        [Test]
        public void WhenAskedToLeaveTheStreamOpenItIsLeftOpen()
        {
            using var tmpDir = new TemporaryDirectory();
            var path = tmpDir.RandomFileName();
            using (FileStream fs = File.OpenWrite(path))
            {
                using (var errorRecordingStream = new ErrorRecordingStream(fs, closeInner: false))
                {
                    errorRecordingStream.WriteString("Hello");
                }

                fs.WriteString(" and this");
            }

            File.ReadAllText(path).Should().Be("Hello and this");
        }
        
        [Test]
        public void ReadErrorsFromUnderlyingStreamAreRecorded()
        {
            int counter = 0;
            var errorRecordingStream = new ErrorRecordingStream(
                new CallBackStream(new MemoryStream("hello".GetBytesUtf8()))
                    .WithBeforeRead((_) => throw new Exception($"Exception number {counter++}"))
                , true
                );

            Assert.Throws<Exception>(() => errorRecordingStream.Read(new byte[100], 0, 100));
            Assert.Throws<Exception>(() => errorRecordingStream.Read(new byte[100], 0, 100));

            errorRecordingStream.WasTheEndOfStreamEncountered.Should().Be(false);
            errorRecordingStream.ReadExceptions.Count.Should().Be(2);
            errorRecordingStream.ReadExceptions[0].Message.Should().Be("Exception number 0");
            errorRecordingStream.ReadExceptions[1].Message.Should().Be("Exception number 1");
        }
        
        [Test]
        public async Task ReadErrorsFromUnderlyingStreamAreRecordedAsync()
        {
            int counter = 0;
            var errorRecordingStream = new ErrorRecordingStream(
                new CallBackStream(new MemoryStream("hello".GetBytesUtf8()))
                    .WithBeforeRead((_) => throw new Exception($"Exception number {counter++}"))
                , true
            );

            await AssertionExtensions.Should(() => errorRecordingStream.ReadAsync(new byte[100], 0, 100)).ThrowAsync<Exception>();
            await AssertionExtensions.Should(() => errorRecordingStream.ReadAsync(new byte[100], 0, 100)).ThrowAsync<Exception>();

            errorRecordingStream.WasTheEndOfStreamEncountered.Should().Be(false);
            errorRecordingStream.ReadExceptions.Count.Should().Be(2);
            errorRecordingStream.ReadExceptions[0].Message.Should().Be("Exception number 0");
            errorRecordingStream.ReadExceptions[1].Message.Should().Be("Exception number 1");
        }
        
        [Test]
        public void EndOfStreamEncounteredIsRecorded()
        {
            int counter = 0;
            var errorRecordingStream = new ErrorRecordingStream(new MemoryStream("hello".GetBytesUtf8()), true);

            while (errorRecordingStream.Read(new byte[100], 0, 100) != 0)
            {
            }

            errorRecordingStream.WasTheEndOfStreamEncountered.Should().Be(true);
            errorRecordingStream.ReadExceptions.Count.Should().Be(0);
        }
        
        [Test]
        public async Task EndOfStreamEncounteredIsRecordedAsync()
        {
            int counter = 0;
            var errorRecordingStream = new ErrorRecordingStream(new MemoryStream("hello".GetBytesUtf8()), true);

            while ((await errorRecordingStream.ReadAsync(new byte[100], 0, 100)) != 0)
            {
            }

            errorRecordingStream.WasTheEndOfStreamEncountered.Should().Be(true);
            errorRecordingStream.ReadExceptions.Count.Should().Be(0);
        }
        
        [Test]
        public async Task ReadingIntoAZeroLengthArrayIsNotAEndOfStream()
        {
            int counter = 0;
            var errorRecordingStream = new ErrorRecordingStream(new MemoryStream("hello".GetBytesUtf8()), true);

            await errorRecordingStream.ReadAsync(new byte[0], 0, 0);
            await errorRecordingStream.ReadAsync(new byte[100], 1, 0);
            errorRecordingStream.Read(new byte[0], 0, 0);
            errorRecordingStream.Read(new byte[100], 1, 0);


            errorRecordingStream.WasTheEndOfStreamEncountered.Should().Be(false);
            errorRecordingStream.ReadExceptions.Count.Should().Be(0);
        }
        
        [Test]
        public void WriteErrorsFromUnderlyingStreamAreRecorded()
        {
            int counter = 0;
            var errorRecordingStream = new ErrorRecordingStream(
                new CallBackStream(new MemoryStream("hello".GetBytesUtf8()))
                    .WithBeforeWrite((_) => throw new Exception($"Exception number {counter++}"))
                , true
            );

            Assert.Throws<Exception>(() => errorRecordingStream.Write(new byte[100], 0, 100));
            Assert.Throws<Exception>(() => errorRecordingStream.Write(new byte[100], 0, 100));

            errorRecordingStream.WasTheEndOfStreamEncountered.Should().Be(false);
            errorRecordingStream.WriteExceptions.Count.Should().Be(2);
            errorRecordingStream.WriteExceptions[0].Message.Should().Be("Exception number 0");
            errorRecordingStream.WriteExceptions[1].Message.Should().Be("Exception number 1");
        }
        
        [Test]
        public async Task WriteErrorsFromUnderlyingStreamAreRecordedAsync()
        {
            int counter = 0;
            var errorRecordingStream = new ErrorRecordingStream(
                new CallBackStream(new MemoryStream("hello".GetBytesUtf8()))
                    .WithBeforeWrite((_) => throw new Exception($"Exception number {counter++}"))
                , true
            );

            await AssertionExtensions.Should(() => errorRecordingStream.WriteAsync(new byte[100], 0, 100)).ThrowAsync<Exception>();
            await AssertionExtensions.Should(() => errorRecordingStream.WriteAsync(new byte[100], 0, 100)).ThrowAsync<Exception>();

            errorRecordingStream.WasTheEndOfStreamEncountered.Should().Be(false);
            errorRecordingStream.WriteExceptions.Count.Should().Be(2);
            errorRecordingStream.WriteExceptions[0].Message.Should().Be("Exception number 0");
            errorRecordingStream.WriteExceptions[1].Message.Should().Be("Exception number 1");
        }
    }
    
    public class ErrorRecordingStreamIsAsyncFixture : StreamWrapperSupportsAsyncIOFixture
    {
        protected override AsyncStream WrapStream(Stream stream) => new ErrorRecordingStream(stream, true);
    }
}