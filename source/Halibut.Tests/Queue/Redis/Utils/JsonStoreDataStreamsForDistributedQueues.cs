using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Queue.QueuedDataStreams;
using Newtonsoft.Json;

namespace Halibut.Tests.Queue.Redis.Utils
{
    /// <summary>
    /// A test implementation of IStoreDataStreamsForDistributedQueues that stores 
    /// data streams as JSON in the metadata string, using base64 encoding for binary data.
    /// </summary>
    public class JsonStoreDataStreamsForDistributedQueues : IStoreDataStreamsForDistributedQueues
    {
        public async Task<string> StoreDataStreams(IReadOnlyList<DataStream> dataStreams, CancellationToken cancellationToken)
        {
            var dataStreamData = new Dictionary<Guid, string>();

            foreach (var dataStream in dataStreams)
            {
                using var memoryStream = new MemoryStream();
                await dataStream.WriteData(memoryStream, cancellationToken);
                var bytes = memoryStream.ToArray();
                var base64Data = Convert.ToBase64String(bytes);
                dataStreamData[dataStream.Id] = base64Data;
            }

            return JsonConvert.SerializeObject(dataStreamData);
        }

        public async Task ReHydrateDataStreams(string dataStreamMetadata, IReadOnlyList<DataStream> dataStreams, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            
            if (string.IsNullOrWhiteSpace(dataStreamMetadata))
            {
                throw new ArgumentException("Data stream metadata cannot be null or empty", nameof(dataStreamMetadata));
            }

            var dataStreamData = JsonConvert.DeserializeObject<Dictionary<Guid, string>>(dataStreamMetadata);
            if (dataStreamData == null)
            {
                throw new InvalidOperationException("Failed to deserialize data stream metadata");
            }

            foreach (var dataStream in dataStreams)
            {
                if (!dataStreamData.TryGetValue(dataStream.Id, out var base64Data))
                {
                    throw new InvalidOperationException($"No stored data found for DataStream with ID: {dataStream.Id}");
                }

                var bytes = Convert.FromBase64String(base64Data);
                dataStream.SetWriterAsync(async (stream, ct) =>
                {
                    await stream.WriteAsync(bytes, ct);
                });
            }
        }
    }
}
