using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.Tests.Util;
using Halibut.Transport.Observability;
using Halibut.Transport.Streams;
using NUnit.Framework;

namespace Halibut.Tests.Transport.Streams
{
    public class WriteIntoMemoryBufferStreamFixture : BaseTest
    {
        [Test]
        public async Task DoesNotWriteToSink_IfBufferingWasNotApplied_WithLimitGreaterThanDataSize([Values]StreamMethod streamMethod)
        {
            // Arrange
            var bytesToWrite = Encoding.ASCII.GetBytes("Some bytes for testing");
            using var memoryStream = new MemoryStream();
            using var sut = new WriteIntoMemoryBufferStream(memoryStream, bytesToWrite.Length + 1, OnDispose.LeaveInputStreamOpen);

            // Act
            await sut.WriteToStream(streamMethod, bytesToWrite, 0, bytesToWrite.Length, CancellationToken);

            // Assert
            memoryStream.Length.Should().Be(0);
            sut.BytesWrittenIntoMemory.Should().Be(bytesToWrite.Length);
        }

        [Test]
        public async Task WriteToSink_IfBufferingWasNotApplied_WithLimitGreaterThanDataSize_WhenDisposed([Values] StreamMethod streamMethod)
        {
            // Arrange
            var bytesToWrite = Encoding.ASCII.GetBytes("Some bytes for testing");
            using var memoryStream = new MemoryStream();
            using (var sut = new WriteIntoMemoryBufferStream(memoryStream, bytesToWrite.Length + 1, OnDispose.LeaveInputStreamOpen))
            {
                // Act
                await sut.WriteToStream(streamMethod, bytesToWrite, 0, bytesToWrite.Length, CancellationToken);

                // Assert
                sut.BytesWrittenIntoMemory.Should().Be(bytesToWrite.Length);
            }
            
            memoryStream.Length.Should().Be(bytesToWrite.Length);
        }

        [Test]
        public async Task WriteToSink_OnlyOne_IfBufferingWasApplied_WithLimitGreaterThanDataSize_AndThenDisposed([Values] StreamMethod streamMethod)
        {
            // Arrange
            var bytesToWrite = Encoding.ASCII.GetBytes("Some bytes for testing");
            using var memoryStream = new MemoryStream();
            using (var sut = new WriteIntoMemoryBufferStream(memoryStream, bytesToWrite.Length + 1, OnDispose.LeaveInputStreamOpen))
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
        public async Task WriteToSink_IfBufferingWasNotApplied_WithLimitLessThanDataSize([Values] StreamMethod streamMethod)
        {
            // Arrange
            var bytesToWrite = Encoding.ASCII.GetBytes("Some bytes for testing");
            using var memoryStream = new MemoryStream();
            using var sut = new WriteIntoMemoryBufferStream(memoryStream, bytesToWrite.Length - 1, OnDispose.LeaveInputStreamOpen);

            // Act
            await sut.WriteToStream(streamMethod, bytesToWrite, 0, bytesToWrite.Length, CancellationToken);

            // Assert
            memoryStream.Length.Should().Be(bytesToWrite.Length);
            sut.BytesWrittenIntoMemory.Should().Be(0);
        }

        [Test]
        public async Task WriteToSink_IfBufferingWasApplied_WithLimitLessThanDataSize([Values] StreamMethod streamMethod)
        {
            // Arrange
            var bytesToWrite = Encoding.ASCII.GetBytes("Some bytes for testing");
            using var memoryStream = new MemoryStream();
            using var sut = new WriteIntoMemoryBufferStream(memoryStream, bytesToWrite.Length - 1, OnDispose.LeaveInputStreamOpen);

            // Act
            await sut.WriteToStream(streamMethod, bytesToWrite, 0, bytesToWrite.Length, CancellationToken);
            await sut.WriteBufferToUnderlyingStream(CancellationToken);

            // Assert
            memoryStream.Length.Should().Be(bytesToWrite.Length);
            sut.BytesWrittenIntoMemory.Should().Be(0);
        }

        [Test]
        public async Task WriteToSink_IfBufferingWasApplied_WithLimitEqualToDataSize([Values] StreamMethod streamMethod)
        {
            // Arrange
            var bytesToWrite = Encoding.ASCII.GetBytes("Some bytes for testing");
            using var memoryStream = new MemoryStream();
            using var sut = new WriteIntoMemoryBufferStream(memoryStream, bytesToWrite.Length, OnDispose.LeaveInputStreamOpen);

            // Act
            await sut.WriteToStream(streamMethod, bytesToWrite, 0, bytesToWrite.Length, CancellationToken);
            await sut.WriteBufferToUnderlyingStream(CancellationToken);

            // Assert
            memoryStream.Length.Should().Be(bytesToWrite.Length);
            sut.BytesWrittenIntoMemory.Should().Be(bytesToWrite.Length);
        }

        [Test]
        public async Task WriteToSink_IfBufferingWasApplied_WithLimitGreaterThanDataSize([Values] StreamMethod streamMethod)
        {
            // Arrange
            var bytesToWrite = Encoding.ASCII.GetBytes("Some bytes for testing");
            using var memoryStream = new MemoryStream();
            using var sut = new WriteIntoMemoryBufferStream(memoryStream, bytesToWrite.Length + 1, OnDispose.LeaveInputStreamOpen);

            // Act
            await sut.WriteToStream(streamMethod, bytesToWrite, 0, bytesToWrite.Length, CancellationToken);
            await sut.WriteBufferToUnderlyingStream(CancellationToken);

            // Assert
            memoryStream.Length.Should().Be(bytesToWrite.Length);
            sut.BytesWrittenIntoMemory.Should().Be(bytesToWrite.Length);
        }
        
        [Test]
        public async Task WriteToSink_IfBufferingWasApplied_WithLimitLessThanDataSize_WritingOneByteAtATime([Values] StreamMethod streamMethod)
        {
            // Arrange
            var bytesToWrite = Encoding.ASCII.GetBytes("Some");
            var writeIntoMemoryLimitBytes = bytesToWrite.Length / 2;
            using var memoryStream = new MemoryStream();
            using var sut = new WriteIntoMemoryBufferStream(memoryStream, writeIntoMemoryLimitBytes, OnDispose.LeaveInputStreamOpen);

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

        
    }
}