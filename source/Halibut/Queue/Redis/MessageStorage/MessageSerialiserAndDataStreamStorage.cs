using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Queue.QueuedDataStreams;
using Halibut.Transport.Protocol;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;

namespace Halibut.Queue.Redis.MessageStorage
{
    public class DataStreamSummary
    {
        public Guid Id;
        public long Length;

        public DataStreamSummary(Guid id, long length)
        {
            this.Id = id;
            Length = length;
        }

        public static DataStreamSummary From(DataStream dataStream) => new(dataStream.Id, dataStream.Length);
    }

    public class MetadataToStore
    {
        public Guid ActivityId { get; set;  }
        public List<DataStreamSummary> DataStreams { get; set; } = new();
        public byte[] DataStreamMetadata { get; set; } = Array.Empty<byte>();

        public MetadataToStore(IEnumerable<DataStreamSummary> dataStreams, byte[] dataStreamMetadata, Guid activityId)
        {
            DataStreams = new List<DataStreamSummary>(dataStreams);
            DataStreamMetadata = dataStreamMetadata;
            ActivityId = activityId;
        }

        public static byte[] Serialize(MetadataToStore metadataToStore)
        {
            using var memoryStream = new MemoryStream();
            using var bsonWriter = new BsonDataWriter(memoryStream);
            var serializer = JsonSerializer.CreateDefault();
            serializer.Serialize(bsonWriter, metadataToStore);
            return memoryStream.ToArray();
        }

        public static MetadataToStore? Deserialize(byte[] bsonData)
        {
            if (bsonData == null || bsonData.Length == 0)
                return null;
                
            using var memoryStream = new MemoryStream(bsonData);
            using var bsonReader = new BsonDataReader(memoryStream);
            var serializer = JsonSerializer.CreateDefault();
            return serializer.Deserialize<MetadataToStore>(bsonReader);
        }
    }
    
    public class MessageSerialiserAndDataStreamStorage : IMessageSerialiserAndDataStreamStorage
    {
        readonly QueueMessageSerializer queueMessageSerializer;
        readonly IStoreDataStreamsForDistributedQueues storeDataStreamsForDistributedQueues;

        public MessageSerialiserAndDataStreamStorage(QueueMessageSerializer queueMessageSerializer, IStoreDataStreamsForDistributedQueues storeDataStreamsForDistributedQueues)
        {
            this.queueMessageSerializer = queueMessageSerializer;
            this.storeDataStreamsForDistributedQueues = storeDataStreamsForDistributedQueues;
        }

        // public static long total = 0;
        public async Task<(RedisStoredMessage, HeartBeatDrivenDataStreamProgressReporter)> PrepareRequest(RequestMessage request, CancellationToken cancellationToken)
        {
            // var sw = Stopwatch.StartNew();
            // for (int i = 0; i < 1000; i++)
            // {
            //     await queueMessageSerializer.PrepareMessageForWireTransferAndForQueue(request);
            // }
            // sw.Stop();
            // Interlocked.Add(ref total, (long) sw.Elapsed.TotalMilliseconds);
            
            var (jsonRequestMessage, dataStreams) = await queueMessageSerializer.PrepareMessageForWireTransferAndForQueue(request);
            SwitchDataStreamsToNotReportProgress(dataStreams);
            var dataStreamProgressReporter = HeartBeatDrivenDataStreamProgressReporter.CreateForDataStreams(dataStreams);
            var dataStreamMetadata = await storeDataStreamsForDistributedQueues.StoreDataStreams(dataStreams, cancellationToken);

            // Create data stream summary list
            var dataStreamSummaries = dataStreams.Select(DataStreamSummary.From);
            var dataStreamSummaryList = new MetadataToStore(dataStreamSummaries, dataStreamMetadata, request.ActivityId);
            var serializedDataStreamSummaryList = MetadataToStore.Serialize(dataStreamSummaryList);
            
            return (new RedisStoredMessage(jsonRequestMessage, serializedDataStreamSummaryList), dataStreamProgressReporter);
        }

        static void SwitchDataStreamsToNotReportProgress(IReadOnlyList<DataStream> dataStreams)
        {
            foreach (var dataStream in dataStreams)
            {
                // It doesn't make sense for this side to be reporting progress, since we are not sending the data to tentacle.
                if (dataStream is ICanBeSwitchedToNotReportProgress canBeSwitchedToNotReportProgress)
                {
                    canBeSwitchedToNotReportProgress.SwitchWriterToNotReportProgress();
                }
            }
        }

        public async Task<(PreparedRequestMessage, RequestDataStreamsTransferProgress)> ReadRequest(RedisStoredMessage storedMessage, CancellationToken cancellationToken)
        {
            var bytesToTransfer = await queueMessageSerializer.ReadBytesForWireTransfer(storedMessage.Message);
            
            var sumr = MetadataToStore.Deserialize(storedMessage.DataStreamMetadata)!;

            var dataStreams = sumr.DataStreams.Select(d => new DataStream()
            {
                Id = d.Id,
                Length = d.Length
            }).ToArray();

            var rehydratableDataStreams = BuildUpRehydratableDataStreams(dataStreams, out var dataStreamTransferProgress);

            await storeDataStreamsForDistributedQueues.RehydrateDataStreams(sumr.DataStreamMetadata, rehydratableDataStreams, cancellationToken);
            return (new PreparedRequestMessage(bytesToTransfer, dataStreams.ToList()), new RequestDataStreamsTransferProgress(dataStreamTransferProgress));
        }

        public async Task<RedisStoredMessage> PrepareResponseForStorageInRedis(Guid activityId, ResponseBytesAndDataStreams response, CancellationToken cancellationToken)
        {
            var responseBytesToStoreInRedis = await queueMessageSerializer.PrepareBytesFromWire(response.ResponseBytes);
            
            
            var dataStreamMetadata = await storeDataStreamsForDistributedQueues.StoreDataStreams(response.DataStreams, cancellationToken);
            
            // Create data stream summary list
            //var dataStreamSummaries = response.DataStreams.Select(DataStreamSummary.From);
            //var dataStreamSummaryList = new MetadataToStore(dataStreamSummaries, dataStreamMetadata, activityId);
            //var serializedDataStreamSummaryList = MetadataToStore.Serialize(dataStreamSummaryList);
            
            return new RedisStoredMessage(responseBytesToStoreInRedis, dataStreamMetadata);
        }

        public static long totalDecompress = 0;
        public async Task<ResponseMessage> ReadResponseFromRedisStoredMessage(RedisStoredMessage storedMessage, CancellationToken cancellationToken)
        {
            // var sw = Stopwatch.StartNew();
            // for (int i = 0; i < 1000; i++)
            // {
            //     await queueMessageSerializer.ConvertStoredResponseToResponseMessage<ResponseMessage>(storedMessage.Message);
            // }
            // sw.Stop();
            // Interlocked.Add(ref totalDecompress, (long) sw.Elapsed.TotalMilliseconds);
            
            var (response, dataStreams) = await queueMessageSerializer.ConvertStoredResponseToResponseMessage<ResponseMessage>(storedMessage.Message);
            
            var rehydratableDataStreams = BuildUpRehydratableDataStreams(dataStreams, out _);
            
            await storeDataStreamsForDistributedQueues.RehydrateDataStreams(storedMessage.DataStreamMetadata, rehydratableDataStreams, cancellationToken);
            return response;
        }
        
        static List<IRehydrateDataStream> BuildUpRehydratableDataStreams(IReadOnlyList<DataStream> dataStreams, out List<RedisDataStreamTransferProgressRecorder> dataStreamTransferProgress)
        {
            var rehydratableDataStreams = new List<IRehydrateDataStream>();
            dataStreamTransferProgress = new List<RedisDataStreamTransferProgressRecorder>();
            foreach (var dataStream in dataStreams)
            {
                var dtp = new RedisDataStreamTransferProgressRecorder(dataStream);
                dataStreamTransferProgress.Add(dtp);
                rehydratableDataStreams.Add(new RehydrateWithProgressReporting(dataStream, dtp));
            }

            return rehydratableDataStreams;
        }
    }
}