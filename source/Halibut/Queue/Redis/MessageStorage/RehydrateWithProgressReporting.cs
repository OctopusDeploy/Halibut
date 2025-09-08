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

        public void Rehydrate(Func<(Stream, IAsyncDisposable?)> data)
        {
            
            dataStream.SetWriterAsync(async (destination, ct) =>
            {
                var sourceAndDisposable = data();
                await using var source = sourceAndDisposable.Item1;
                try
                {
                    var streamCopier = new StreamCopierWithProgress(source, dataStreamTransferProgress);
                    await streamCopier.CopyAndReportProgressAsync(destination, ct);
                }
                finally
                {
                    if(sourceAndDisposable.Item2 != null) await sourceAndDisposable.Item2.DisposeAsync();
                }
            });
        }
    }
}