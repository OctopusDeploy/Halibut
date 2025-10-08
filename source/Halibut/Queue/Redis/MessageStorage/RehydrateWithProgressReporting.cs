
using System;
using Halibut.DataStreams;

namespace Halibut.Queue.Redis.MessageStorage
{
    public class RehydrateWithProgressReporting : IRehydrateDataStream
    {
        readonly DataStream dataStream;
        readonly IDataStreamTransferProgress dataStreamTransferProgress;
        readonly bool useReceiver;

        public RehydrateWithProgressReporting(DataStream dataStream, IDataStreamTransferProgress dataStreamTransferProgress, bool useReceiver)
        {
            this.dataStream = dataStream;
            this.dataStreamTransferProgress = dataStreamTransferProgress;
            this.useReceiver = useReceiver;
        }

        public Guid Id => dataStream.Id;
        public long Length => dataStream.Length;

        public void Rehydrate(Func<DataStreamRehydrationData> dataStreamRehydrationDataSupplier)
        {
            if (useReceiver)
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