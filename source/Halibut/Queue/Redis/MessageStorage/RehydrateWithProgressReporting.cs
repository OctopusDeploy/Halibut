
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

        public void Rehydrate(bool useReciever, Func<DataStreamRehydrationData> dataStreamRehydrationDataSupplier)
        {
            if (useReciever)
            {
                // This is very sus, since if we have a receiver, then we already have the data, so there is no progress to report on.
                ((IDataStreamInternal) dataStream).Received(new DataStreamRehydrationDataDataStreamReceiver(dataStreamRehydrationDataSupplier));
            }
            else
            {
                dataStream.SetWriterAsync(async (destination, ct) => 
                {
                    await using var dataStreamRehydrationData = dataStreamRehydrationDataSupplier();
                    var streamCopier = new StreamCopierWithProgress(dataStreamRehydrationData.Data, dataStreamTransferProgress);
                    await streamCopier.CopyAndReportProgressAsync(destination, ct);
                });
            }
        }
    }
}