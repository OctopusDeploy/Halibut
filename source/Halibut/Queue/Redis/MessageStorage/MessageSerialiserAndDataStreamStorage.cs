using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Queue.QueuedDataStreams;
using Halibut.Transport.Protocol;

namespace Halibut.Queue.Redis.MessageStorage
{
    public class MessageSerialiserAndDataStreamStorage : IMessageSerialiserAndDataStreamStorage
    {
        readonly QueueMessageSerializer queueMessageSerializer;
        readonly IStoreDataStreamsForDistributedQueues storeDataStreamsForDistributedQueues;

        public MessageSerialiserAndDataStreamStorage(QueueMessageSerializer queueMessageSerializer, IStoreDataStreamsForDistributedQueues storeDataStreamsForDistributedQueues)
        {
            this.queueMessageSerializer = queueMessageSerializer;
            this.storeDataStreamsForDistributedQueues = storeDataStreamsForDistributedQueues;
        }

        public async Task<(RedisStoredMessage, HeartBeatDrivenDataStreamProgressReporter)> PrepareRequest(RequestMessage request, CancellationToken cancellationToken)
        {
            var (jsonRequestMessage, dataStreams) = await queueMessageSerializer.WriteMessage(request);
            SwitchDataStreamsToNotReportProgress(dataStreams);
            var dataStreamProgressReporter = HeartBeatDrivenDataStreamProgressReporter.CreateForDataStreams(dataStreams);
            var dataStreamMetadata = await storeDataStreamsForDistributedQueues.StoreDataStreams(dataStreams, false, cancellationToken);
            return (new RedisStoredMessage(jsonRequestMessage, dataStreamMetadata), dataStreamProgressReporter);
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

        public async Task<(RequestMessage, RequestDataStreamsTransferProgress)> ReadRequest(RedisStoredMessage storedMessage, CancellationToken cancellationToken)
        {
            var (request, dataStreams) = await queueMessageSerializer.ReadMessage<RequestMessage>(storedMessage.Message);

            var rehydratableDataStreams = BuildUpRehydratableDataStreams(false, dataStreams, out var dataStreamTransferProgress);

            await storeDataStreamsForDistributedQueues.RehydrateDataStreams(storedMessage.DataStreamMetadata, rehydratableDataStreams, cancellationToken);
            return (request, new RequestDataStreamsTransferProgress(dataStreamTransferProgress));
        }

        public async Task<RedisStoredMessage> PrepareResponse(ResponseMessage response, CancellationToken cancellationToken)
        {
            var (jsonResponseMessage, dataStreams) = await queueMessageSerializer.WriteMessage(response);
            var dataStreamMetadata = await storeDataStreamsForDistributedQueues.StoreDataStreams(dataStreams, true, cancellationToken);
            return new RedisStoredMessage(jsonResponseMessage, dataStreamMetadata);
        }
        
        public async Task<ResponseMessage> ReadResponse(RedisStoredMessage storedMessage, CancellationToken cancellationToken)
        {
            var (response, dataStreams) = await queueMessageSerializer.ReadMessage<ResponseMessage>(storedMessage.Message);
            
            var rehydratableDataStreams = BuildUpRehydratableDataStreams(true, dataStreams, out _);
            
            await storeDataStreamsForDistributedQueues.RehydrateDataStreams(storedMessage.DataStreamMetadata, rehydratableDataStreams, cancellationToken);
            return response;
        }
        
        static List<IRehydrateDataStream> BuildUpRehydratableDataStreams(bool useReceiver, IReadOnlyList<DataStream> dataStreams, out List<RedisDataStreamTransferProgressRecorder> dataStreamTransferProgress)
        {
            var rehydratableDataStreams = new List<IRehydrateDataStream>();
            dataStreamTransferProgress = new List<RedisDataStreamTransferProgressRecorder>();
            foreach (var dataStream in dataStreams)
            {
                var dtp = new RedisDataStreamTransferProgressRecorder(dataStream);
                dataStreamTransferProgress.Add(dtp);
                rehydratableDataStreams.Add(new RehydrateWithProgressReporting(dataStream, dtp, useReceiver));
            }

            return rehydratableDataStreams;
        }
    }
}