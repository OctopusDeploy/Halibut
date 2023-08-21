using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Tests.Support;
using Halibut.Tests.Support.Streams;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Util;
using Halibut.Transport.Streams;
using NUnit.Framework;

namespace Halibut.Tests.Transport.Streams
{
    public class WriteIntoMemoryBufferStreamFixture : BaseTest
    {
        [Test]
        [StreamMethodTestCase]
        public async Task DoesNotWriteToSink_IfBufferingWasNotApplied_WithLimitGreaterThanDataSize(StreamMethod streamMethod)
        {
            // Arrange
            var bytesToWrite = Encoding.ASCII.GetBytes("Some bytes for testing");
            using var memoryStream = new MemoryStream();
            await using var sut = new WriteIntoMemoryBufferStream(memoryStream, bytesToWrite.Length + 1, OnDispose.LeaveInputStreamOpen);

            // Act
            await sut.WriteToStream(streamMethod, bytesToWrite, 0, bytesToWrite.Length, CancellationToken);

            // Assert
            memoryStream.Length.Should().Be(0);
            sut.BytesWrittenIntoMemory.Should().Be(bytesToWrite.Length);
        }

        [Test]
        [StreamMethodTestCase]
        public async Task WriteToSink_IfBufferingWasNotApplied_WithLimitGreaterThanDataSize_WhenDisposed(StreamMethod streamMethod)
        {
            // Arrange
            var bytesToWrite = Encoding.ASCII.GetBytes("Some bytes for testing");
            using var memoryStream = new MemoryStream();
            await using (var sut = new WriteIntoMemoryBufferStream(memoryStream, bytesToWrite.Length + 1, OnDispose.LeaveInputStreamOpen))
            {
                // Act
                await sut.WriteToStream(streamMethod, bytesToWrite, 0, bytesToWrite.Length, CancellationToken);

                // Assert
                sut.BytesWrittenIntoMemory.Should().Be(bytesToWrite.Length);
            }
            
            memoryStream.Length.Should().Be(bytesToWrite.Length);
        }

        [Test]
        [StreamMethodTestCase]
        public async Task WriteToSink_OnlyOne_IfBufferingWasApplied_WithLimitGreaterThanDataSize_AndThenDisposed(StreamMethod streamMethod)
        {
            // Arrange
            var bytesToWrite = Encoding.ASCII.GetBytes("Some bytes for testing");
            using var memoryStream = new MemoryStream();
            await using (var sut = new WriteIntoMemoryBufferStream(memoryStream, bytesToWrite.Length + 1, OnDispose.LeaveInputStreamOpen))
            {
                // Act
                await sut.WriteToStream(streamMethod, bytesToWrite, 0, bytesToWrite.Length, CancellationToken);
                await sut.WriteBufferToUnderlyingStream(CancellationToken);

                // Assert
                sut.BytesWrittenIntoMemory.Should().Be(bytesToWrite.Length);
            }
            
            memoryStream.Length.Should().Be(bytesToWrite.Length);
        }

        [Test]
        [StreamMethodTestCase]
        public async Task WriteToSink_IfBufferingWasNotApplied_WithLimitLessThanDataSize(StreamMethod streamMethod)
        {
            // Arrange
            var bytesToWrite = Encoding.ASCII.GetBytes("Some bytes for testing");
            using var memoryStream = new MemoryStream();
            await using var sut = new WriteIntoMemoryBufferStream(memoryStream, bytesToWrite.Length - 1, OnDispose.LeaveInputStreamOpen);

            // Act
            await sut.WriteToStream(streamMethod, bytesToWrite, 0, bytesToWrite.Length, CancellationToken);

            // Assert
            memoryStream.Length.Should().Be(bytesToWrite.Length);
            sut.BytesWrittenIntoMemory.Should().Be(0);
        }

        [Test]
        [StreamMethodTestCase]
        public async Task WriteToSink_IfBufferingWasApplied_WithLimitLessThanDataSize(StreamMethod streamMethod)
        {
            // Arrange
            var bytesToWrite = Encoding.ASCII.GetBytes("Some bytes for testing");
            using var memoryStream = new MemoryStream();
            await using var sut = new WriteIntoMemoryBufferStream(memoryStream, bytesToWrite.Length - 1, OnDispose.LeaveInputStreamOpen);

            // Act
            await sut.WriteToStream(streamMethod, bytesToWrite, 0, bytesToWrite.Length, CancellationToken);
            await sut.WriteBufferToUnderlyingStream(CancellationToken);

            // Assert
            memoryStream.Length.Should().Be(bytesToWrite.Length);
            sut.BytesWrittenIntoMemory.Should().Be(0);
        }

        [Test]
        [StreamMethodTestCase]
        public async Task WriteToSink_IfBufferingWasApplied_WithLimitEqualToDataSize(StreamMethod streamMethod)
        {
            // Arrange
            var bytesToWrite = Encoding.ASCII.GetBytes("Some bytes for testing");
            using var memoryStream = new MemoryStream();
            await using var sut = new WriteIntoMemoryBufferStream(memoryStream, bytesToWrite.Length, OnDispose.LeaveInputStreamOpen);

            // Act
            await sut.WriteToStream(streamMethod, bytesToWrite, 0, bytesToWrite.Length, CancellationToken);
            await sut.WriteBufferToUnderlyingStream(CancellationToken);

            // Assert
            memoryStream.Length.Should().Be(bytesToWrite.Length);
            sut.BytesWrittenIntoMemory.Should().Be(bytesToWrite.Length);
        }

        [Test]
        [StreamMethodTestCase]
        public async Task WriteToSink_IfBufferingWasApplied_WithLimitGreaterThanDataSize(StreamMethod streamMethod)
        {
            // Arrange
            var bytesToWrite = Encoding.ASCII.GetBytes("Some bytes for testing");
            using var memoryStream = new MemoryStream();
            await using var sut = new WriteIntoMemoryBufferStream(memoryStream, bytesToWrite.Length + 1, OnDispose.LeaveInputStreamOpen);

            // Act
            await sut.WriteToStream(streamMethod, bytesToWrite, 0, bytesToWrite.Length, CancellationToken);
            await sut.WriteBufferToUnderlyingStream(CancellationToken);

            // Assert
            memoryStream.Length.Should().Be(bytesToWrite.Length);
            sut.BytesWrittenIntoMemory.Should().Be(bytesToWrite.Length);
        }
        
        [Test]
        [StreamMethodTestCase]
        public async Task WriteToSink_IfBufferingWasApplied_WithLimitLessThanDataSize_WritingOneByteAtATime(StreamMethod streamMethod)
        {
            // Arrange
            var bytesToWrite = Encoding.ASCII.GetBytes("Some");
            var writeIntoMemoryLimitBytes = bytesToWrite.Length / 2;
            using var memoryStream = new MemoryStream();
            await using var sut = new WriteIntoMemoryBufferStream(memoryStream, writeIntoMemoryLimitBytes, OnDispose.LeaveInputStreamOpen);

            // Act
            for (int i = 0; i < bytesToWrite.Length; i++)
            {
                await sut.WriteToStream(streamMethod, bytesToWrite, i, 1, CancellationToken);
            }
            
            await sut.WriteBufferToUnderlyingStream(CancellationToken);

            // Assert
            memoryStream.Length.Should().Be(bytesToWrite.Length);
            sut.BytesWrittenIntoMemory.Should().Be(writeIntoMemoryLimitBytes);
        }
        
        [Test]
        public async Task AfterAFailedWriteDisposeShouldNotAttemptToWriteThoseBytesAgain()
        {
            using var memoryStream = new MemoryStream();
            using var actionBeforeWriteStream = new ActionBeforeWriteStream(memoryStream);
            actionBeforeWriteStream.BeforeWrite = () => throw new Exception("Oh no");
            var sut = new WriteIntoMemoryBufferStream(actionBeforeWriteStream, 8192, OnDispose.LeaveInputStreamOpen);

            sut.WriteString("Some");

            await AssertAsync.Throws<Exception>(async () => await sut.WriteBufferToUnderlyingStream(CancellationToken));
            
            memoryStream.Length.Should().Be(0);

            // This should not throw since it should not attempt to write the same bytes to the stream. 
            await sut.DisposeAsync();
        }
    }
}