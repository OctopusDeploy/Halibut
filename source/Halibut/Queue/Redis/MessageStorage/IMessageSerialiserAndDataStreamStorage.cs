using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Queue.QueuedDataStreams;
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
        Task<(RedisStoredMessage, HeartBeatDrivenDataStreamProgressReporter)> PrepareRequest(RequestMessage request, CancellationToken cancellationToken);
        Task<(PreparedRequestMessage, RequestDataStreamsTransferProgress)> ReadRequest(RedisStoredMessage jsonRequest, CancellationToken cancellationToken);
        Task<RedisStoredMessage> PrepareResponseForStorageInRedis(Guid activityId, ResponseBytesAndDataStreams response, CancellationToken cancellationToken);
        Task<ResponseMessage> ReadResponseFromRedisStoredMessage(RedisStoredMessage jsonResponse, CancellationToken cancellationToken);
    }
}