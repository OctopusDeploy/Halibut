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

        public async Task<string> PrepareRequest(RequestMessage request, CancellationToken cancellationToken)
        {
            var (payload, dataStreams) = queueMessageSerializer.WriteMessage(request);
            await storeDataStreamsForDistributedQueues.StoreDataStreams(dataStreams, cancellationToken);
            return payload;
        }
        
        public async Task<RequestMessage> ReadRequest(string jsonRequest, CancellationToken cancellationToken)
        {
            var (request, dataStreams) = queueMessageSerializer.ReadMessage<RequestMessage>(jsonRequest);
            await storeDataStreamsForDistributedQueues.ReHydrateDataStreams(dataStreams, cancellationToken);
            return request;
        }
        
        public async Task<string> PrepareResponse(ResponseMessage response, CancellationToken cancellationToken)
        {
            var (payload, dataStreams) = queueMessageSerializer.WriteMessage(response);
            await storeDataStreamsForDistributedQueues.StoreDataStreams(dataStreams, cancellationToken);
            return payload;
        }
        
        public async Task<ResponseMessage> ReadResponse(string jsonResponse, CancellationToken cancellationToken)
        {
            var (response, dataStreams) = queueMessageSerializer.ReadMessage<ResponseMessage>(jsonResponse);
            await storeDataStreamsForDistributedQueues.ReHydrateDataStreams(dataStreams, cancellationToken);
            return response;
        }
    }
}