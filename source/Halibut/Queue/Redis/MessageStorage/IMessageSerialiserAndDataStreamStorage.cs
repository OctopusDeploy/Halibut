using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Queue.Redis.RedisHelpers;
using Halibut.Transport.Protocol;

namespace Halibut.Queue.Redis.MessageStorage
{
    /// <summary>
    /// Deals with preparing the request/response messages for storage in the
    /// Redis Queue and helps with reading from the queue.
    ///
    /// This takes care of serialising the message into something that can be stored in
    /// Redis, and calls IStoreDataStreamsForDistributedQueues for storage/retrievable
    /// of DataStreams.
    /// </summary>
    public interface IMessageSerialiserAndDataStreamStorage
    {
        Task<RedisStoredMessage> PrepareRequest(RequestMessage request, CancellationToken cancellationToken);
        Task<RequestMessage> ReadRequest(RedisStoredMessage jsonRequest, CancellationToken cancellationToken);
        Task<RedisStoredMessage> PrepareResponse(ResponseMessage response, CancellationToken cancellationToken);
        Task<ResponseMessage> ReadResponse(RedisStoredMessage jsonResponse, CancellationToken cancellationToken);
    }
}