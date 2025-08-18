
#if NET8_0_OR_GREATER
using System;
using System.Threading;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace Halibut.Queue.Redis
{
    public interface IHalibutRedisTransport
    {
        Task<IAsyncDisposable> SubscribeToRequestMessagePulseChannel(Uri endpoint, Action<ChannelMessage> onRequestMessagePulse, CancellationToken cancellationToken);
        Task PulseRequestPushedToEndpoint(Uri endpoint, CancellationToken cancellationToken);
        Task PushRequestGuidOnToQueue(Uri endpoint, Guid guid, CancellationToken cancellationToken);
        Task<Guid?> TryPopNextRequestGuid(Uri endpoint, CancellationToken cancellationToken);
        Task PutRequest(Uri endpoint, Guid requestId, string payload, TimeSpan requestPickupTimeout, CancellationToken cancellationToken);
        Task<string?> TryGetAndRemoveRequest(Uri endpoint, Guid requestId, CancellationToken cancellationToken);
        Task<bool> IsRequestStillOnQueue(Uri endpoint, Guid requestId, CancellationToken cancellationToken);

        Task<IAsyncDisposable> SubscribeToRequestCancellation(Uri endpoint, Guid request,
            Func<Task> onCancellationReceived,
            CancellationToken cancellationToken);

        Task PublishCancellation(Uri endpoint, Guid requestId, CancellationToken cancellationToken);
        string RequestCancelledMarkerKey(Uri endpoint, Guid requestId);
        Task MarkRequestAsCancelled(Uri endpoint, Guid requestId, TimeSpan ttl, CancellationToken cancellationToken);
        Task<bool> IsRequestMarkedAsCancelled(Uri endpoint, Guid requestId, CancellationToken cancellationToken);

        Task<IAsyncDisposable> SubscribeToNodeHeartBeatChannel(
            Uri endpoint, 
            Guid request,
            HalibutQueueNodeSendingPulses nodeSendingPulsesType,
            Func<Task> onHeartBeat,
            CancellationToken cancellationToken);

        Task SendHeartBeatFromNodeProcessingTheRequest(Uri endpoint, Guid requestId, HalibutQueueNodeSendingPulses nodeSendingPulsesType, CancellationToken cancellationToken);
        Task SendHeartBeatFromNodeProcessingTheRequest(Uri endpoint, Guid requestId, CancellationToken cancellationToken);

        Task<IAsyncDisposable> SubscribeToNodeProcessingTheRequestHeartBeatChannel(
            Uri endpoint, 
            Guid request,
            Func<Task> onHeartBeat,
            CancellationToken cancellationToken);

        Task<IAsyncDisposable> SubscribeToResponseChannel(Uri endpoint, Guid identifier,
            Func<string, Task> onValueReceived,
            CancellationToken cancellationToken);

        Task PublishThatResponseIsAvailable(Uri endpoint, Guid identifier, string value, CancellationToken cancellationToken);
        Task MarkThatResponseIsSet(Uri endpoint, Guid identifier, string value, TimeSpan ttl, CancellationToken cancellationToken);
        Task<string?> GetResponseMessage(Uri endpoint, Guid identifier, CancellationToken cancellationToken);
        Task<bool> DeleteResponse(Uri endpoint, Guid identifier, CancellationToken cancellationToken);
    }
}
#endif