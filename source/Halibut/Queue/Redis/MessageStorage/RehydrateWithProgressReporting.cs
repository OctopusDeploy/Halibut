
using System;
using System.IO;
using Halibut.DataStreams;

namespace Halibut.Queue.Redis.MessageStorage
{
    public class RehydrateWithProgressReporting : IRehydrateDataStream
    {
        readonly DataStream dataStream;
        readonly IDataStreamTransferProgress dataStreamTransferProgress;

        public RehydrateWithProgressReporting(DataStream dataStream, IDataStreamTransferProgress dataStreamTransferProgress)
        {
            this.dataStream = dataStream;
            this.dataStreamTransferProgress = dataStreamTransferProgress;
        }

        public Guid Id => dataStream.Id;
        public long Length => dataStream.Length;

        public void Rehydrate(Func<DataStreamRehydrationData> data)
        {
            
            dataStream.SetWriterAsync(async (destination, ct) => 
            {
                await using var dataStreamRehydrationData = data();
                var streamCopier = new StreamCopierWithProgress(dataStreamRehydrationData.Data, dataStreamTransferProgress);
                await streamCopier.CopyAndReportProgressAsync(destination, ct);
            });
        }
    }
}