using System;
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

        public async Task<RedisStoredMessage> PrepareRequest(RequestMessage request, CancellationToken cancellationToken)
        {
            var (jsonRequestMessage, dataStreams) = queueMessageSerializer.WriteMessage(request);
            var dataStreamMetadata = await storeDataStreamsForDistributedQueues.StoreDataStreams(dataStreams, cancellationToken);
            return new RedisStoredMessage(jsonRequestMessage, dataStreamMetadata);
        }
        
        public async Task<RequestMessage> ReadRequest(RedisStoredMessage storedMessage, CancellationToken cancellationToken)
        {
            var (request, dataStreams) = queueMessageSerializer.ReadMessage<RequestMessage>(storedMessage.Message);
            await storeDataStreamsForDistributedQueues.RehydrateDataStreams(storedMessage.DataStreamMetadata, dataStreams, cancellationToken);
            return request;
        }
        
        public async Task<RedisStoredMessage> PrepareResponse(ResponseMessage response, CancellationToken cancellationToken)
        {
            var (jsonResponseMessage, dataStreams) = queueMessageSerializer.WriteMessage(response);
            var dataStreamMetaData = await storeDataStreamsForDistributedQueues.StoreDataStreams(dataStreams, cancellationToken);
            return new RedisStoredMessage(jsonResponseMessage, dataStreamMetaData);
        }
        
        public async Task<ResponseMessage> ReadResponse(RedisStoredMessage storedMessage, CancellationToken cancellationToken)
        {
            var (response, dataStreams) = queueMessageSerializer.ReadMessage<ResponseMessage>(storedMessage.Message);
            await storeDataStreamsForDistributedQueues.RehydrateDataStreams(storedMessage.DataStreamMetadata, dataStreams, cancellationToken);
            return response;
        }
    }
}