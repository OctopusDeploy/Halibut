using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.Util;
using Halibut.Transport.Streams;
using NUnit.Framework;

namespace Halibut.Tests.Transport.Streams
{
    public class ReadIntoMemoryBufferStreamFixture : BaseTest
    {
        [Test]
        [StreamMethodTestCase]
        public async Task ReadsFromSourceStream_IfBufferingWasNotApplied(StreamMethod streamMethod)
        {
            // Arrange
            var bytesToWrite = Encoding.ASCII.GetBytes("Some bytes for testing");
            using var memoryStream = CreateMemoryStreamPopulatedWith(bytesToWrite);
            await using var sut = new ReadIntoMemoryBufferStream(memoryStream, 1000, OnDispose.LeaveInputStreamOpen);

            // Act
            var readBuffer = new byte[bytesToWrite.Length];
            var bytesRead = await sut.ReadFromStream(streamMethod, readBuffer, 0, bytesToWrite.Length, CancellationToken);
            
            // Assert
            bytesRead.Should().Be(bytesToWrite.Length);
            readBuffer.Should().BeEquivalentTo(bytesToWrite);
            sut.BytesReadIntoMemory.Should().Be(0);
        }
        
        [Test]
        [StreamMethodTestCase]
        public async Task ReadsFromMemoryBuffer_IfBufferingWasApplied_WithLimitEqualToDataSize(StreamMethod streamMethod)
        {
            // Arrange
            var bytesToWrite = Encoding.ASCII.GetBytes("Some bytes for testing");
            using var memoryStream = CreateMemoryStreamPopulatedWith(bytesToWrite);
            await using var sut = new ReadIntoMemoryBufferStream(memoryStream, bytesToWrite.Length, OnDispose.LeaveInputStreamOpen);

            await sut.BufferIntoMemoryFromSourceStreamUntilLimitReached(CancellationToken);

            // Act
            var readBuffer = new byte[bytesToWrite.Length];
            var bytesRead = await sut.ReadFromStream(streamMethod, readBuffer, 0, bytesToWrite.Length, CancellationToken);

            // Assert
            bytesRead.Should().Be(bytesToWrite.Length);
            readBuffer.Should().BeEquivalentTo(bytesToWrite);
            sut.BytesReadIntoMemory.Should().Be(bytesToWrite.Length);
        }

        [Test]
        [StreamMethodTestCase]
        public async Task ReadsFromMemoryBuffer_IfBufferingWasApplied_WithLimitGreaterThanDataSize(StreamMethod streamMethod)
        {
            // Arrange
            var bytesToWrite = Encoding.ASCII.GetBytes("Some bytes for testing");
            using var memoryStream = CreateMemoryStreamPopulatedWith(bytesToWrite);
            await using var sut = new ReadIntoMemoryBufferStream(memoryStream, bytesToWrite.Length + 1, OnDispose.LeaveInputStreamOpen);

            await sut.BufferIntoMemoryFromSourceStreamUntilLimitReached(CancellationToken);

            // Act
            var readBuffer = new byte[bytesToWrite.Length];
            var bytesRead = await sut.ReadFromStream(streamMethod, readBuffer, 0, bytesToWrite.Length, CancellationToken);

            // Assert
            bytesRead.Should().Be(bytesToWrite.Length);
            readBuffer.Should().BeEquivalentTo(bytesToWrite);
            sut.BytesReadIntoMemory.Should().Be(bytesToWrite.Length);
        }

        [Test]
        [StreamMethodTestCase]
        public async Task PartiallyReadsFromMemoryBuffer_IfBufferingWasApplied_WithLimitLessThanDataSize(StreamMethod streamMethod)
        {
            // Arrange
            var bytesToWrite = Encoding.ASCII.GetBytes("Some bytes for testing");
            using var memoryStream = CreateMemoryStreamPopulatedWith(bytesToWrite);
            await using var sut = new ReadIntoMemoryBufferStream(memoryStream, bytesToWrite.Length - 1, OnDispose.LeaveInputStreamOpen);

            await sut.BufferIntoMemoryFromSourceStreamUntilLimitReached(CancellationToken);

            // Act
            var readBuffer = new byte[bytesToWrite.Length];
            // First round read till we hit the memory buffer
            var bytesRead = await sut.ReadFromStream(streamMethod, readBuffer, 0, bytesToWrite.Length, CancellationToken);
            bytesRead.Should().Be(bytesToWrite.Length - 1);

            // Next round finishes the data from the source
            bytesRead = await sut.ReadFromStream(streamMethod, readBuffer, bytesToWrite.Length - 1, 1, CancellationToken);
            bytesRead.Should().Be(1);

            // Assert
            readBuffer.Should().BeEquivalentTo(bytesToWrite);
            sut.BytesReadIntoMemory.Should().Be(bytesToWrite.Length - 1);
        }
        
        [Test]
        [StreamMethodTestCase]
        public async Task PartiallyReadsFromMemoryBuffer_IfBufferingWasApplied_ReadingOneByteAtATime(StreamMethod streamMethod)
        {
            // Arrange
            var bytesToWrite = Encoding.ASCII.GetBytes("Some bytes for testing");
            var readIntoMemoryLimitBytes = bytesToWrite.Length / 2;
            using var memoryStream = CreateMemoryStreamPopulatedWith(bytesToWrite);
            await using var sut = new ReadIntoMemoryBufferStream(memoryStream, readIntoMemoryLimitBytes, OnDispose.LeaveInputStreamOpen);

            await sut.BufferIntoMemoryFromSourceStreamUntilLimitReached(CancellationToken);

            // Act
            var readBuffer = new byte[bytesToWrite.Length];
            for (int i = 0; i < bytesToWrite.Length; i++)
            {
                var bytesRead = await sut.ReadFromStream(streamMethod, readBuffer, i, 1, CancellationToken);
                bytesRead.Should().Be(1);
            }

            // Assert
            readBuffer.Should().BeEquivalentTo(bytesToWrite);
            sut.BytesReadIntoMemory.Should().Be(readIntoMemoryLimitBytes);
        }

        static MemoryStream CreateMemoryStreamPopulatedWith(byte[] bytesToWrite)
        {
            var memoryStream = new MemoryStream();
            
            memoryStream.Write(bytesToWrite, 0, bytesToWrite.Length);
            memoryStream.Position = 0;

            return memoryStream;
        }
    }
}