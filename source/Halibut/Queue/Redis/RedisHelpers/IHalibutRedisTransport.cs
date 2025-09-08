
#if NET8_0_OR_GREATER
using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Queue.Redis.MessageStorage;
using Halibut.Queue.Redis.NodeHeartBeat;
using StackExchange.Redis;

namespace Halibut.Queue.Redis.RedisHelpers
{
    public interface IHalibutRedisTransport
    {
        Task<IAsyncDisposable> SubscribeToRequestMessagePulseChannel(Uri endpoint, Action<ChannelMessage> onRequestMessagePulse, CancellationToken cancellationToken);
        Task PulseRequestPushedToEndpoint(Uri endpoint, CancellationToken cancellationToken);
        
        
        Task PushRequestGuidOnToQueue(Uri endpoint, Guid guid, CancellationToken cancellationToken);
        Task<Guid?> TryPopNextRequestGuid(Uri endpoint, CancellationToken cancellationToken);
        
        
        Task PutRequest(Uri endpoint, Guid requestId, RedisStoredMessage requestMessage, TimeSpan requestPickupTimeout, CancellationToken cancellationToken);
        Task<RedisStoredMessage?> TryGetAndRemoveRequest(Uri endpoint, Guid requestId, CancellationToken cancellationToken);
        Task<bool> IsRequestStillOnQueue(Uri endpoint, Guid requestId, CancellationToken cancellationToken);

        Task<IAsyncDisposable> SubscribeToRequestCancellation(
            Uri endpoint,
            Guid requestId,
            Func<Task> onRpcCancellation,
            CancellationToken cancellationToken);
        Task PublishCancellation(Uri endpoint, Guid requestId, CancellationToken cancellationToken);
        
        Task MarkRequestAsCancelled(Uri endpoint, Guid requestId, TimeSpan ttl, CancellationToken cancellationToken);
        Task<bool> IsRequestMarkedAsCancelled(Uri endpoint, Guid requestId, CancellationToken cancellationToken);

        
        Task<IAsyncDisposable> SubscribeToNodeHeartBeatChannel(
            Uri endpoint, 
            Guid requestId,
            HalibutQueueNodeSendingPulses nodeSendingPulsesType,
            Func<string, Task> onHeartBeat,
            CancellationToken cancellationToken);

        Task SendNodeHeartBeat(Uri endpoint, Guid requestId, HalibutQueueNodeSendingPulses nodeSendingPulsesType, string nodeHeartBeatMessage, CancellationToken cancellationToken);
        
        
        Task<IAsyncDisposable> SubscribeToResponseChannel(
            Uri endpoint,
            Guid identifier,
            Func<string, Task> onValueReceived,
            CancellationToken cancellationToken);
        Task PublishThatResponseIsAvailable(Uri endpoint, Guid identifier, CancellationToken cancellationToken);
        
        
        Task SetResponseMessage(Uri endpoint, Guid identifier, RedisStoredMessage responseMessage, TimeSpan ttl, CancellationToken cancellationToken);
        Task<RedisStoredMessage?> GetResponseMessage(Uri endpoint, Guid identifier, CancellationToken cancellationToken);
        Task DeleteResponseMessage(Uri endpoint, Guid identifier, CancellationToken cancellationToken);
    }
}
#endif