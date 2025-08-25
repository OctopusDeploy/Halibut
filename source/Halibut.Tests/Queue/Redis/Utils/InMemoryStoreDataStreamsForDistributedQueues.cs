using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Queue.QueuedDataStreams;

namespace Halibut.Tests.Queue.Redis.Utils
{
    public class InMemoryStoreDataStreamsForDistributedQueues : IStoreDataStreamsForDistributedQueues
    {
        readonly IDictionary<Guid, byte[]> dataStreamsStored = new Dictionary<Guid, byte[]>();
        public async Task<string> StoreDataStreams(IReadOnlyList<DataStream> dataStreams, CancellationToken cancellationToken)
        {
            foreach (var dataStream in dataStreams)
            {
                using var memoryStream = new MemoryStream();
                await dataStream.WriteData(memoryStream, cancellationToken);
                dataStreamsStored[dataStream.Id] = memoryStream.ToArray();
            }

            return "";
        }

        public async Task ReHydrateDataStreams(string dataStreamMetadata, IReadOnlyList<DataStream> dataStreams, CancellationToken cancellationToken)
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