using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Transport.Observability;
using Halibut.Transport.Streams;
using NUnit.Framework;

namespace Halibut.Tests.Transport.Streams
{
    public class ReadIntoMemoryBufferStreamFixture : BaseTest
    {
        [Test]
        public async Task ReadsFromSourceStream_IfBufferingWasNotApplied([Values]StreamMethod streamMethod)
        {
            // Arrange
            var bytesToWrite = Encoding.ASCII.GetBytes("Some bytes for testing");
            using var memoryStream = CreateMemoryStreamPopulatedWith(bytesToWrite);
            using var sut = new ReadIntoMemoryBufferStream(memoryStream, 1000, OnDispose.LeaveInputStreamOpen);

            // Act
            var readBuffer = new byte[bytesToWrite.Length];
            var bytesRead = await ReadFromStream(streamMethod, sut, readBuffer, 0, bytesToWrite.Length);
            
            // Assert
            bytesRead.Should().Be(bytesToWrite.Length);
            readBuffer.Should().BeEquivalentTo(bytesToWrite);
            sut.BytesReadIntoMemory.Should().Be(0);
        }
        
        [Test]
        public async Task ReadsFromMemoryBuffer_IfBufferingWasApplied_WithLimitEqualToDataSize([Values] StreamMethod streamMethod)
        {
            // Arrange
            var bytesToWrite = Encoding.ASCII.GetBytes("Some bytes for testing");
            using var memoryStream = CreateMemoryStreamPopulatedWith(bytesToWrite);
            using var sut = new ReadIntoMemoryBufferStream(memoryStream, bytesToWrite.Length, OnDispose.LeaveInputStreamOpen);

            await sut.BufferIntoMemoryFromSourceStreamUntilLimitReached(CancellationToken);

            // Act
            var readBuffer = new byte[bytesToWrite.Length];
            var bytesRead = await ReadFromStream(streamMethod, sut, readBuffer, 0, bytesToWrite.Length);

            // Assert
            bytesRead.Should().Be(bytesToWrite.Length);
            readBuffer.Should().BeEquivalentTo(bytesToWrite);
            sut.BytesReadIntoMemory.Should().Be(bytesToWrite.Length);
        }

        [Test]
        public async Task ReadsFromMemoryBuffer_IfBufferingWasApplied_WithLimitGreaterThanDataSize([Values] StreamMethod streamMethod)
        {
            // Arrange
            var bytesToWrite = Encoding.ASCII.GetBytes("Some bytes for testing");
            using var memoryStream = CreateMemoryStreamPopulatedWith(bytesToWrite);
            using var sut = new ReadIntoMemoryBufferStream(memoryStream, bytesToWrite.Length + 1, OnDispose.LeaveInputStreamOpen);

            await sut.BufferIntoMemoryFromSourceStreamUntilLimitReached(CancellationToken);

            // Act
            var readBuffer = new byte[bytesToWrite.Length];
            var bytesRead = await ReadFromStream(streamMethod, sut, readBuffer, 0, bytesToWrite.Length);

            // Assert
            bytesRead.Should().Be(bytesToWrite.Length);
            readBuffer.Should().BeEquivalentTo(bytesToWrite);
            sut.BytesReadIntoMemory.Should().Be(bytesToWrite.Length);
        }

        [Test]
        public async Task PartiallyReadsFromMemoryBuffer_IfBufferingWasApplied_WithLimitLessThanDataSize([Values] StreamMethod streamMethod)
        {
            // Arrange
            var bytesToWrite = Encoding.ASCII.GetBytes("Some bytes for testing");
            using var memoryStream = CreateMemoryStreamPopulatedWith(bytesToWrite);
            using var sut = new ReadIntoMemoryBufferStream(memoryStream, bytesToWrite.Length - 1, OnDispose.LeaveInputStreamOpen);

            await sut.BufferIntoMemoryFromSourceStreamUntilLimitReached(CancellationToken);

            // Act
            var readBuffer = new byte[bytesToWrite.Length];
            // First round read till we hit the memory buffer
            var bytesRead = await ReadFromStream(streamMethod, sut, readBuffer, 0, bytesToWrite.Length);
            bytesRead.Should().Be(bytesToWrite.Length - 1);

            // Next round finishes the data from the source
            bytesRead = await ReadFromStream(streamMethod, sut, readBuffer, bytesToWrite.Length - 1, 1);
            bytesRead.Should().Be(1);

            // Assert
            readBuffer.Should().BeEquivalentTo(bytesToWrite);
            sut.BytesReadIntoMemory.Should().Be(bytesToWrite.Length - 1);
        }
        
        [Test]
        public async Task PartiallyReadsFromMemoryBuffer_IfBufferingWasApplied_ReadingOneByteAtATime([Values] StreamMethod streamMethod)
        {
            // Arrange
            var bytesToWrite = Encoding.ASCII.GetBytes("Some bytes for testing");
            var readIntoMemoryLimitBytes = bytesToWrite.Length / 2;
            using var memoryStream = CreateMemoryStreamPopulatedWith(bytesToWrite);
            using var sut = new ReadIntoMemoryBufferStream(memoryStream, readIntoMemoryLimitBytes, OnDispose.LeaveInputStreamOpen);

            await sut.BufferIntoMemoryFromSourceStreamUntilLimitReached(CancellationToken);

            // Act
            var readBuffer = new byte[bytesToWrite.Length];
            for (int i = 0; i < bytesToWrite.Length; i++)
            {
                var bytesRead = await ReadFromStream(streamMethod, sut, readBuffer, i, 1);
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

        async Task<int> ReadFromStream(StreamMethod streamMethod, ReadIntoMemoryBufferStream sut, byte[] readBuffer, int offset, int count)
        {
            switch (streamMethod)
            {
                case StreamMethod.Async:
                    return await sut.ReadAsync(readBuffer, offset, count, CancellationToken);
                case StreamMethod.Sync:
                    return sut.Read(readBuffer, offset, count);
                case StreamMethod.LegacyAsync:
                    // This is the way async reading was done in earlier version of .NET
                    int bytesRead = -1;
                    sut.BeginRead(readBuffer, offset, count, AsyncCallback, sut);
                    void AsyncCallback(IAsyncResult result)
                    {
                        bytesRead = sut.EndRead(result);
                    }

                    while (bytesRead < 0 && !CancellationToken.IsCancellationRequested)
                    {
                        await Task.Delay(10);
                    }
                    return bytesRead;
                default:
                    throw new ArgumentOutOfRangeException(nameof(streamMethod), streamMethod, null);
            }
        }
    }
}