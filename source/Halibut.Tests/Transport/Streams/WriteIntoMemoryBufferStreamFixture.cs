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
            await WriteToStream(streamMethod, sut, bytesToWrite, 0, bytesToWrite.Length);

            // Assert
            memoryStream.Length.Should().Be(0);
            sut.BytesWrittenIntoMemory.Should().Be(bytesToWrite.Length);
        }

        [Test]
        public async Task WriteToSink_IfBufferingWasNotApplied_WithLimitLessThanDataSize([Values] StreamMethod streamMethod)
        {
            // Arrange
            var bytesToWrite = Encoding.ASCII.GetBytes("Some bytes for testing");
            using var memoryStream = new MemoryStream();
            using var sut = new WriteIntoMemoryBufferStream(memoryStream, bytesToWrite.Length - 1, OnDispose.LeaveInputStreamOpen);

            // Act
            await WriteToStream(streamMethod, sut, bytesToWrite, 0, bytesToWrite.Length);

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
            await WriteToStream(streamMethod, sut, bytesToWrite, 0, bytesToWrite.Length);
            await sut.WriteAnyUnwrittenDataToSinkStream(CancellationToken);

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
            await WriteToStream(streamMethod, sut, bytesToWrite, 0, bytesToWrite.Length);
            await sut.WriteAnyUnwrittenDataToSinkStream(CancellationToken);

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
            await WriteToStream(streamMethod, sut, bytesToWrite, 0, bytesToWrite.Length);
            await sut.WriteAnyUnwrittenDataToSinkStream(CancellationToken);

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
                await WriteToStream(streamMethod, sut, bytesToWrite, i, 1);
            }
            
            await sut.WriteAnyUnwrittenDataToSinkStream(CancellationToken);

            // Assert
            memoryStream.Length.Should().Be(bytesToWrite.Length);
            sut.BytesWrittenIntoMemory.Should().Be(writeIntoMemoryLimitBytes);
        }

        async Task WriteToStream(StreamMethod streamMethod, WriteIntoMemoryBufferStream sut, byte[] buffer, int offset, int count)
        {
            switch (streamMethod)
            {
                case StreamMethod.Async:
                    await sut.WriteAsync(buffer, offset, count, CancellationToken);
                    return;
                case StreamMethod.Sync:
                    sut.Write(buffer, offset, count);
                    return;
                case StreamMethod.BeginEnd:
                    var beginWriteResult = sut.BeginWrite(buffer, offset, count, AsyncCallback, sut);
                    void AsyncCallback(IAsyncResult result)
                    {
                        sut.EndWrite(result);
                    }

                    beginWriteResult.AsyncWaitHandle.WaitOne().Should().BeTrue();
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(streamMethod), streamMethod, null);
            }
        }
    }
}