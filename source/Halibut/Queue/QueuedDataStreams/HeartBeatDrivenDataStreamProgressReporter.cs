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

        readonly HashSet<Guid> completedDataStreams = new();

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
                lock (completedDataStreams)
                {
                    if(completedDataStreams.Contains(keyValuePair.Key)) continue;
                }
                
                
                if (dataStreamsToReportProgressOn.TryGetValue(keyValuePair.Key, out var dataStreamWithTransferProgress))
                {
                    var progress = dataStreamWithTransferProgress.DataStreamTransferProgress;
                    await progress.Progress(keyValuePair.Value, cancellationToken);
                    
                    if (dataStreamWithTransferProgress.Length == keyValuePair.Value)
                    {
                        lock (completedDataStreams)
                        {
                            completedDataStreams.Add(keyValuePair.Key);
                        }
                    }
                }
            }
        }
        
        public static HeartBeatDrivenDataStreamProgressReporter CreateForDataStreams(IEnumerable<DataStream> dataStreams)
        {
            var dataStreamsToReportProgressOn = dataStreams.OfType<IDataStreamWithFileUploadProgress>().ToArray().ToImmutableDictionary(d => d.Id);
            return new HeartBeatDrivenDataStreamProgressReporter(dataStreamsToReportProgressOn);
        }

        public async ValueTask DisposeAsync()
        {
            // Since we may not get a HeartBeatMessage stating that the DataStreams have been completely transferred,
            // this object is disposable and on dispose we note that file will no longer be uploading. Which
            // for the normal percentage based file transfer progress will result in marking the DataStreams as 100% uploaded.
            // If we don't do this at the end of a successful call we may find DataStream progress is reported as less than 100%.
            var localCopyCompletedDataStreams = new List<Guid>();
            
            // Because of where this is used, it is hard to be sure the completedDataStreams won't be modified while disposing,
            // so take a copy of streams to work with.
            lock (completedDataStreams)
            {
                localCopyCompletedDataStreams.AddRange(completedDataStreams);
            }
            foreach (var keyValuePair in dataStreamsToReportProgressOn)
            {
                if (!localCopyCompletedDataStreams.Contains(keyValuePair.Key))
                {
                    var progress = keyValuePair.Value.DataStreamTransferProgress;
                    // This may be called twice if HeartBeats are received while disposing.
                    await progress.NoLongerUploading(CancellationToken.None);
                }
            }
        }
    }
}