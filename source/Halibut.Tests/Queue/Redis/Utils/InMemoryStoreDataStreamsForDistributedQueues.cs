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
        public async Task<byte[]> StoreDataStreams(IReadOnlyList<DataStream> dataStreams, bool useReceiver, CancellationToken cancellationToken)
        {
            foreach (var dataStream in dataStreams)
            {
                using var memoryStream = new MemoryStream();
                if (useReceiver)
                {
                    await dataStream.Receiver().ReadAsync(async (stream, ct) =>
                    {
#if NET8_0_OR_GREATER
                        await stream.CopyToAsync(memoryStream, ct);
#else
                        throw new NotImplementedException("Redis PRQ is not supported in net48");
#endif
                    }, cancellationToken);
                }
                else
                {
                    await dataStream.WriteData(memoryStream, cancellationToken);
                }
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