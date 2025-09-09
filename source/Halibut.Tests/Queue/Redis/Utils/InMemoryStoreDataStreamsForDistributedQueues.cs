using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Queue.QueuedDataStreams;
using Halibut.Queue.Redis.MessageStorage;

namespace Halibut.Tests.Queue.Redis.Utils
{
    public class InMemoryStoreDataStreamsForDistributedQueues : IStoreDataStreamsForDistributedQueues
    {
        readonly IDictionary<Guid, byte[]> dataStreamsStored = new Dictionary<Guid, byte[]>();
        public async Task<byte[]> StoreDataStreams(IReadOnlyList<DataStream> dataStreams, CancellationToken cancellationToken)
        {
            foreach (var dataStream in dataStreams)
            {
                using var memoryStream = new MemoryStream();
                await dataStream.WriteData(memoryStream, cancellationToken);
                dataStreamsStored[dataStream.Id] = memoryStream.ToArray();
            }

            return Array.Empty<byte>();
        }
        
        public async Task RehydrateDataStreams(byte[] dataStreamMetadata, List<IRehydrateDataStream> dataStreams, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            foreach (var dataStream in dataStreams)
            {
                var bytes = dataStreamsStored[dataStream.Id];
                dataStreamsStored.Remove(dataStream.Id);
                dataStream.Rehydrate(() =>
                {
                    var s = new MemoryStream(bytes);
                    return new DataStreamRehydrationData(s);
                });
            }
        }
    }
}