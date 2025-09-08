using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Halibut.DataStreams;

namespace Halibut.Queue.QueuedDataStreams
{
    /// <summary>
    /// Update the progress of IDataStreamWithFileUploadProgress based on the HeartBeat data received.
    /// </summary>
    public class HeartBeatDrivenDataStreamProgressReporter : IAsyncDisposable, IGetNotifiedOfHeartBeats
    {
        readonly ImmutableDictionary<Guid, IDataStreamWithFileUploadProgress> dataStreamsToReportProgressOn;

        readonly ConcurrentBag<Guid> completedDataStreams = new();

        HeartBeatDrivenDataStreamProgressReporter(ImmutableDictionary<Guid, IDataStreamWithFileUploadProgress> dataStreamsToReportProgressOn)
        {
            this.dataStreamsToReportProgressOn = dataStreamsToReportProgressOn;
        }

        public async Task HeartBeatReceived(HeartBeatMessage heartBeatMessage, CancellationToken cancellationToken)
        {
            if(dataStreamsToReportProgressOn.IsEmpty) return;
            if(heartBeatMessage.DataStreamProgress == null) return;
            
            foreach (var keyValuePair in heartBeatMessage.DataStreamProgress)
            {
                if(completedDataStreams.Contains(keyValuePair.Key)) continue;
                
                if (dataStreamsToReportProgressOn.TryGetValue(keyValuePair.Key, out var dataStreamWithTransferProgress))
                {
                    var progress = dataStreamWithTransferProgress.DataStreamTransferProgress;
                    await progress.Progress(keyValuePair.Value, cancellationToken);
                    
                    if (dataStreamWithTransferProgress.Length == keyValuePair.Value)
                    {
                        completedDataStreams.Add(keyValuePair.Key);
                    }
                }
            }
        }

        // TODO rename to CreateForDataStreams
        public static HeartBeatDrivenDataStreamProgressReporter FromDataStreams(IEnumerable<DataStream> dataStreams)
        {
            var dataStreamsToReportProgressOn = dataStreams.OfType<IDataStreamWithFileUploadProgress>().ToArray().ToImmutableDictionary(d => d.Id);
            return new HeartBeatDrivenDataStreamProgressReporter(dataStreamsToReportProgressOn);
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var keyValuePair in dataStreamsToReportProgressOn)
            {
                if (!completedDataStreams.Contains(keyValuePair.Key))
                {
                    var progress = keyValuePair.Value.DataStreamTransferProgress;
                    await progress.NoLongerUploading(CancellationToken.None);
                    completedDataStreams.Add(keyValuePair.Key);
                }
            }
        }
    }
}