using System;
using System.Collections.Generic;
using System.IO;
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
        public async Task HeartBeatReceived_WithSingleDataStream_CallsUpdateProgressAsyncWithPercentageComplete()
        {
            // Arrange
            const int streamSize = 100;
            var progressUpdates = new List<int>();
            var progressUpdateCalls = 0;
            
            // Create a mock stream of size 100 bytes
            var mockStream = new MemoryStream(new byte[streamSize]);
            
            // Create the updateProgressAsync function that captures progress updates
            Task UpdateProgressAsync(int percentageComplete, CancellationToken ct)
            {
                progressUpdates.Add(percentageComplete);
                Interlocked.Increment(ref progressUpdateCalls);
                return Task.CompletedTask;
            }
            
            // Create a DataStream using FromStream with our progress callback
            var dataStream = DataStream.FromStream(mockStream, UpdateProgressAsync);
            
            // Create the HeartBeatDrivenDataStreamProgressReporter with our single DataStream
            var progressReporter = HeartBeatDrivenDataStreamProgressReporter.CreateForDataStreams(new[] { dataStream });
            
            // Create heart beat messages with different progress values
            var heartBeatMessage25 = new HeartBeatMessage
            {
                DataStreamProgress = new Dictionary<Guid, long>
                {
                    { dataStream.Id, 25 } // 25% complete (25 out of 100 bytes)
                }
            };
            
            var heartBeatMessage50 = new HeartBeatMessage
            {
                DataStreamProgress = new Dictionary<Guid, long>
                {
                    { dataStream.Id, 50 } // 50% complete (50 out of 100 bytes)
                }
            };
            
            var heartBeatMessage100 = new HeartBeatMessage
            {
                DataStreamProgress = new Dictionary<Guid, long>
                {
                    { dataStream.Id, 100 } // 100% complete (100 out of 100 bytes)
                }
            };
            
            // Act
            await progressReporter.HeartBeatReceived(heartBeatMessage25, CancellationToken.None);
            await progressReporter.HeartBeatReceived(heartBeatMessage50, CancellationToken.None);
            await progressReporter.HeartBeatReceived(heartBeatMessage100, CancellationToken.None);
            
            // Assert
            progressUpdateCalls.Should().Be(3, "updateProgressAsync should be called for each heart beat");
            progressUpdates.Should().ContainInOrder(25, 50, 100);
            
            // Clean up
            await progressReporter.DisposeAsync();
        }
        
        [Test]
        public async Task OnDisposeTheProgressShouldBeMarkedAs100PercentComplete()
        {
            // Arrange
            const int streamSize = 100;
            var progressUpdates = new List<int>();
            var progressUpdateCalls = 0;
            
            // Create a mock stream of size 100 bytes
            var mockStream = new MemoryStream(new byte[streamSize]);
            
            // Create the updateProgressAsync function that captures progress updates
            Task UpdateProgressAsync(int percentageComplete, CancellationToken ct)
            {
                progressUpdates.Add(percentageComplete);
                Interlocked.Increment(ref progressUpdateCalls);
                return Task.CompletedTask;
            }
            
            // Create a DataStream using FromStream with our progress callback
            var dataStream = DataStream.FromStream(mockStream, UpdateProgressAsync);
            
            // Create the HeartBeatDrivenDataStreamProgressReporter with our single DataStream
            var progressReporter = HeartBeatDrivenDataStreamProgressReporter.CreateForDataStreams(new[] { dataStream });
            
            // Create heart beat messages with different progress values
            var heartBeatMessage25 = new HeartBeatMessage
            {
                DataStreamProgress = new Dictionary<Guid, long>
                {
                    { dataStream.Id, 25 } // 25% complete (25 out of 100 bytes)
                }
            };
            
            await progressReporter.HeartBeatReceived(heartBeatMessage25, CancellationToken.None);
            
            // Act
            await progressReporter.DisposeAsync();
            
            
            // Assert
            progressUpdates.Should().ContainInOrder(25, 100); // We should still receive 100% complete, since
                                                              // on dispose we want to let the callback know it is done 
        }
    }
}
