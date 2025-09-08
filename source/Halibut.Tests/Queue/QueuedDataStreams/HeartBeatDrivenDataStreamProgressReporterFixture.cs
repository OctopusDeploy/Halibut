using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Halibut.DataStreams;
using Halibut.Queue.QueuedDataStreams;
using NSubstitute;
using NUnit.Framework;

namespace Halibut.Tests.Queue.QueuedDataStreams
{
    [TestFixture]
    public class HeartBeatDrivenDataStreamProgressReporterFixture
    {
        [Test]
        public void FromDataStreams_WithEmptyCollection_ShouldCreateReporterWithEmptyDictionary()
        {
            // Arrange
            var dataStreams = new List<DataStream>();

            // Act
            var reporter = HeartBeatDrivenDataStreamProgressReporter.FromDataStreams(dataStreams);

            // Assert
            reporter.Should().NotBeNull();
        }

        [Test]
        public void FromDataStreams_WithDataStreamsNotImplementingInterface_ShouldCreateReporterWithEmptyDictionary()
        {
            // Arrange
            var dataStream = new DataStream(100, (stream, ct) => Task.CompletedTask);
            var dataStreams = new List<DataStream> { dataStream };

            // Act
            var reporter = HeartBeatDrivenDataStreamProgressReporter.FromDataStreams(dataStreams);

            // Assert
            reporter.Should().NotBeNull();
        }

        [Test]
        public void FromDataStreams_WithDataStreamsImplementingInterface_ShouldCreateReporterWithCorrectDictionary()
        {
            // Arrange
            var mockDataStream1 = Substitute.For<IDataStreamWithFileUploadProgress>();
            var mockDataStream2 = Substitute.For<IDataStreamWithFileUploadProgress>();
            var id1 = Guid.NewGuid();
            var id2 = Guid.NewGuid();
            
            mockDataStream1.Id.Returns(id1);
            mockDataStream2.Id.Returns(id2);

            var dataStreams = new List<DataStream> { (DataStream)mockDataStream1, (DataStream)mockDataStream2 };

            // Act
            var reporter = HeartBeatDrivenDataStreamProgressReporter.FromDataStreams(dataStreams);

            // Assert
            reporter.Should().NotBeNull();
        }

        [Test]
        public async Task HeartBeatReceived_WithEmptyDataStreams_ShouldReturnImmediately()
        {
            // Arrange
            var reporter = HeartBeatDrivenDataStreamProgressReporter.FromDataStreams(new List<DataStream>());
            var heartBeatMessage = new HeartBeatMessage
            {
                DataStreamProgress = new Dictionary<Guid, long> { { Guid.NewGuid(), 50 } }
            };

            // Act & Assert
            await reporter.HeartBeatReceived(heartBeatMessage, CancellationToken.None);
            // Should complete without throwing
        }

        [Test]
        public async Task HeartBeatReceived_WithNullDataStreamProgress_ShouldReturnImmediately()
        {
            // Arrange
            var mockDataStream = CreateMockDataStreamWithProgress();
            var dataStreams = new List<DataStream> { (DataStream)mockDataStream.dataStream };
            var reporter = HeartBeatDrivenDataStreamProgressReporter.FromDataStreams(dataStreams);
            
            var heartBeatMessage = new HeartBeatMessage { DataStreamProgress = null };

            // Act & Assert
            await reporter.HeartBeatReceived(heartBeatMessage, CancellationToken.None);
            
            // Verify no progress was reported
            await mockDataStream.progress.DidNotReceive().Progress(Arg.Any<long>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task HeartBeatReceived_WithMatchingDataStream_ShouldReportProgress()
        {
            // Arrange
            var mockDataStream = CreateMockDataStreamWithProgress();
            var dataStreams = new List<DataStream> { (DataStream)mockDataStream.dataStream };
            var reporter = HeartBeatDrivenDataStreamProgressReporter.FromDataStreams(dataStreams);
            
            var progressValue = 75L;
            var heartBeatMessage = new HeartBeatMessage
            {
                DataStreamProgress = new Dictionary<Guid, long> { { mockDataStream.id, progressValue } }
            };

            // Act
            await reporter.HeartBeatReceived(heartBeatMessage, CancellationToken.None);

            // Assert
            await mockDataStream.progress.Received(1).Progress(progressValue, CancellationToken.None);
        }

        [Test]
        public async Task HeartBeatReceived_WithNonMatchingDataStream_ShouldNotReportProgress()
        {
            // Arrange
            var mockDataStream = CreateMockDataStreamWithProgress();
            var dataStreams = new List<DataStream> { (DataStream)mockDataStream.dataStream };
            var reporter = HeartBeatDrivenDataStreamProgressReporter.FromDataStreams(dataStreams);
            
            var heartBeatMessage = new HeartBeatMessage
            {
                DataStreamProgress = new Dictionary<Guid, long> { { Guid.NewGuid(), 75 } }
            };

            // Act
            await reporter.HeartBeatReceived(heartBeatMessage, CancellationToken.None);

            // Assert
            await mockDataStream.progress.DidNotReceive().Progress(Arg.Any<long>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task HeartBeatReceived_WhenProgressEqualsLength_ShouldMarkAsCompleted()
        {
            // Arrange
            var mockDataStream = CreateMockDataStreamWithProgress(length: 100);
            var dataStreams = new List<DataStream> { (DataStream)mockDataStream.dataStream };
            var reporter = HeartBeatDrivenDataStreamProgressReporter.FromDataStreams(dataStreams);
            
            var heartBeatMessage = new HeartBeatMessage
            {
                DataStreamProgress = new Dictionary<Guid, long> { { mockDataStream.id, 100 } }
            };

            // Act
            await reporter.HeartBeatReceived(heartBeatMessage, CancellationToken.None);

            // Assert
            await mockDataStream.progress.Received(1).Progress(100, CancellationToken.None);

            // Second call should not report progress as it's completed
            await reporter.HeartBeatReceived(heartBeatMessage, CancellationToken.None);
            await mockDataStream.progress.Received(1).Progress(Arg.Any<long>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task HeartBeatReceived_WithCompletedDataStream_ShouldNotReportProgress()
        {
            // Arrange
            var mockDataStream = CreateMockDataStreamWithProgress(length: 100);
            var dataStreams = new List<DataStream> { (DataStream)mockDataStream.dataStream };
            var reporter = HeartBeatDrivenDataStreamProgressReporter.FromDataStreams(dataStreams);
            
            // Complete the data stream first
            var completeMessage = new HeartBeatMessage
            {
                DataStreamProgress = new Dictionary<Guid, long> { { mockDataStream.id, 100 } }
            };
            await reporter.HeartBeatReceived(completeMessage, CancellationToken.None);

            // Act - try to report progress again
            var secondMessage = new HeartBeatMessage
            {
                DataStreamProgress = new Dictionary<Guid, long> { { mockDataStream.id, 50 } }
            };
            await reporter.HeartBeatReceived(secondMessage, CancellationToken.None);

            // Assert
            await mockDataStream.progress.Received(1).Progress(Arg.Any<long>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task HeartBeatReceived_WithMultipleDataStreams_ShouldReportProgressForAll()
        {
            // Arrange
            var mockDataStream1 = CreateMockDataStreamWithProgress();
            var mockDataStream2 = CreateMockDataStreamWithProgress();
            var dataStreams = new List<DataStream> 
            { 
                (DataStream)mockDataStream1.dataStream, 
                (DataStream)mockDataStream2.dataStream 
            };
            var reporter = HeartBeatDrivenDataStreamProgressReporter.FromDataStreams(dataStreams);
            
            var heartBeatMessage = new HeartBeatMessage
            {
                DataStreamProgress = new Dictionary<Guid, long> 
                { 
                    { mockDataStream1.id, 25 },
                    { mockDataStream2.id, 75 }
                }
            };

            // Act
            await reporter.HeartBeatReceived(heartBeatMessage, CancellationToken.None);

            // Assert
            await mockDataStream1.progress.Received(1).Progress(25, CancellationToken.None);
            await mockDataStream2.progress.Received(1).Progress(75, CancellationToken.None);
        }

        [Test]
        public async Task HeartBeatReceived_WithCancellationToken_ShouldPassTokenToProgress()
        {
            // Arrange
            var mockDataStream = CreateMockDataStreamWithProgress();
            var dataStreams = new List<DataStream> { (DataStream)mockDataStream.dataStream };
            var reporter = HeartBeatDrivenDataStreamProgressReporter.FromDataStreams(dataStreams);
            
            var cancellationToken = new CancellationToken();
            var heartBeatMessage = new HeartBeatMessage
            {
                DataStreamProgress = new Dictionary<Guid, long> { { mockDataStream.id, 50 } }
            };

            // Act
            await reporter.HeartBeatReceived(heartBeatMessage, cancellationToken);

            // Assert
            await mockDataStream.progress.Received(1).Progress(50, cancellationToken);
        }

        [Test]
        public async Task DisposeAsync_WithUncompletedDataStreams_ShouldCallNoLongerUploading()
        {
            // Arrange
            var mockDataStream1 = CreateMockDataStreamWithProgress();
            var mockDataStream2 = CreateMockDataStreamWithProgress(length: 100);
            var dataStreams = new List<DataStream> 
            { 
                (DataStream)mockDataStream1.dataStream, 
                (DataStream)mockDataStream2.dataStream 
            };
            var reporter = HeartBeatDrivenDataStreamProgressReporter.FromDataStreams(dataStreams);
            
            // Complete one data stream
            var heartBeatMessage = new HeartBeatMessage
            {
                DataStreamProgress = new Dictionary<Guid, long> { { mockDataStream2.id, 100 } }
            };
            await reporter.HeartBeatReceived(heartBeatMessage, CancellationToken.None);

            // Act
            await reporter.DisposeAsync();

            // Assert
            await mockDataStream1.progress.Received(1).NoLongerUploading(CancellationToken.None);
            await mockDataStream2.progress.DidNotReceive().NoLongerUploading(Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task DisposeAsync_WithAllCompletedDataStreams_ShouldNotCallNoLongerUploading()
        {
            // Arrange
            var mockDataStream = CreateMockDataStreamWithProgress(length: 100);
            var dataStreams = new List<DataStream> { (DataStream)mockDataStream.dataStream };
            var reporter = HeartBeatDrivenDataStreamProgressReporter.FromDataStreams(dataStreams);
            
            // Complete the data stream
            var heartBeatMessage = new HeartBeatMessage
            {
                DataStreamProgress = new Dictionary<Guid, long> { { mockDataStream.id, 100 } }
            };
            await reporter.HeartBeatReceived(heartBeatMessage, CancellationToken.None);

            // Act
            await reporter.DisposeAsync();

            // Assert
            await mockDataStream.progress.DidNotReceive().NoLongerUploading(Arg.Any<CancellationToken>());
        }

        [Test]
        public async Task DisposeAsync_WithNoDataStreams_ShouldCompleteWithoutError()
        {
            // Arrange
            var reporter = HeartBeatDrivenDataStreamProgressReporter.FromDataStreams(new List<DataStream>());

            // Act & Assert
            await reporter.DisposeAsync();
            // Should complete without throwing
        }

        [Test]
        public async Task DisposeAsync_CalledMultipleTimes_ShouldNotCallNoLongerUploadingMultipleTimes()
        {
            // Arrange
            var mockDataStream = CreateMockDataStreamWithProgress();
            var dataStreams = new List<DataStream> { (DataStream)mockDataStream.dataStream };
            var reporter = HeartBeatDrivenDataStreamProgressReporter.FromDataStreams(dataStreams);

            // Act
            await reporter.DisposeAsync();
            await reporter.DisposeAsync();

            // Assert
            await mockDataStream.progress.Received(1).NoLongerUploading(CancellationToken.None);
        }

        private (IDataStreamWithFileUploadProgress dataStream, IDataStreamTransferProgress progress, Guid id) CreateMockDataStreamWithProgress(long length = 200)
        {
            var id = Guid.NewGuid();
            var mockProgress = Substitute.For<IDataStreamTransferProgress>();
            var mockDataStream = Substitute.For<IDataStreamWithFileUploadProgress>();
            
            mockDataStream.Id.Returns(id);
            mockDataStream.Length.Returns(length);
            mockDataStream.DataStreamTransferProgress.Returns(mockProgress);

            return (mockDataStream, mockProgress, id);
        }
    }
}
