
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Queue.QueuedDataStreams
{
    public class InMemoryStoreDataStreamsForDistributedQueues : IStoreDataStreamsForDistributedQueues
    {
        public IDictionary<Guid, byte[]> dataStreamsStored = new Dictionary<Guid, byte[]>();
        public async Task StoreDataStreams(IReadOnlyList<DataStream> dataStreams, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            
            foreach (var dataStream in dataStreams)
            {
                using var memoryStream = new MemoryStream();
                await dataStream.WriteData(memoryStream, cancellationToken);
                dataStreamsStored[dataStream.Id] = memoryStream.ToArray();
            }
        }

        public async Task ReHydrateDataStreams(IReadOnlyList<DataStream> dataStreams, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            foreach (var dataStream in dataStreams)
            {
                var bytes = dataStreamsStored[dataStream.Id];
                dataStreamsStored.Remove(dataStream.Id);
                dataStream.SetWriterAsync(async (stream, ct) =>
                {
                    await stream.WriteAsync(bytes, 0, bytes.Length, ct);
                });
            }
        }
    }
}