using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Queue.QueuedDataStreams
{
    public class DataStreamProgressReporter : IAsyncDisposable, IGetNotifiedOfHeartBeats
    {
        readonly ImmutableDictionary<Guid, DataStream> dataStreamsToReportProgressOn;

        readonly ConcurrentBag<Guid> completedDataStreams = new();

        DataStreamProgressReporter(ImmutableDictionary<Guid, DataStream> dataStreamsToReportProgressOn)
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
                
                if (dataStreamsToReportProgressOn.TryGetValue(keyValuePair.Key, out var dataStream))
                {
                    var dataStreamWithTransferProgress = (IDataStreamWithFileUploadProgress)dataStream;
                    var progress = dataStreamWithTransferProgress.DataStreamTransferProgress;
                    if (dataStream.Length == keyValuePair.Value)
                    {
                        await progress.UploadComplete(cancellationToken);
                        completedDataStreams.Add(keyValuePair.Key);
                    }
                    else
                    {
                        await progress.Progress(keyValuePair.Value, dataStream.Length, cancellationToken);
                    }
                }
            }
        }

        public static DataStreamProgressReporter FromDataStreams(IEnumerable<DataStream> dataStreams)
        {
            var dataStreamsToReportProgressOn = dataStreams.Where(d => d is IDataStreamWithFileUploadProgress).ToImmutableDictionary(d => d.Id);
            return new DataStreamProgressReporter(dataStreamsToReportProgressOn);
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var keyValuePair in dataStreamsToReportProgressOn)
            {
                if (!completedDataStreams.Contains(keyValuePair.Key))
                {
                    var progress = ((IDataStreamWithFileUploadProgress)keyValuePair.Value).DataStreamTransferProgress;
                    await progress.UploadComplete(CancellationToken.None);
                    completedDataStreams.Add(keyValuePair.Key);
                }
            }
        }
    }
}