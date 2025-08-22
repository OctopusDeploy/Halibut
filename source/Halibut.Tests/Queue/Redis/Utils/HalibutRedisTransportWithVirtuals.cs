
#if NET8_0_OR_GREATER
using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Queue.Redis;
using Halibut.Queue.Redis.NodeHeartBeat;
using StackExchange.Redis;

namespace Halibut.Tests.Queue.Redis.Utils
{
    public class HalibutRedisTransportWithVirtuals : IHalibutRedisTransport
    {
        readonly IHalibutRedisTransport halibutRedisTransport;

        public HalibutRedisTransportWithVirtuals(IHalibutRedisTransport halibutRedisTransport)
        {
            this.halibutRedisTransport = halibutRedisTransport;
        }

        public Task<IAsyncDisposable> SubscribeToRequestMessagePulseChannel(Uri endpoint, Action<ChannelMessage> onRequestMessagePulse, CancellationToken cancellationToken)
        {
            return halibutRedisTransport.SubscribeToRequestMessagePulseChannel(endpoint, onRequestMessagePulse, cancellationToken);
        }

        public Task PulseRequestPushedToEndpoint(Uri endpoint, CancellationToken cancellationToken)
        {
            return halibutRedisTransport.PulseRequestPushedToEndpoint(endpoint, cancellationToken);
        }

        public Task PushRequestGuidOnToQueue(Uri endpoint, Guid guid, CancellationToken cancellationToken)
        {
            return halibutRedisTransport.PushRequestGuidOnToQueue(endpoint, guid, cancellationToken);
        }

        public Task<Guid?> TryPopNextRequestGuid(Uri endpoint, CancellationToken cancellationToken)
        {
            return halibutRedisTransport.TryPopNextRequestGuid(endpoint, cancellationToken);
        }

        public virtual Task PutRequest(Uri endpoint, Guid requestId, string requestMessage, TimeSpan requestPickupTimeout, CancellationToken cancellationToken)
        {
            return halibutRedisTransport.PutRequest(endpoint, requestId, requestMessage, requestPickupTimeout, cancellationToken);
        }

        public Task<string?> TryGetAndRemoveRequest(Uri endpoint, Guid requestId, CancellationToken cancellationToken)
        {
            return halibutRedisTransport.TryGetAndRemoveRequest(endpoint, requestId, cancellationToken);
        }

        public Task<bool> IsRequestStillOnQueue(Uri endpoint, Guid requestId, CancellationToken cancellationToken)
        {
            return halibutRedisTransport.IsRequestStillOnQueue(endpoint, requestId, cancellationToken);
        }

        public Task<IAsyncDisposable> SubscribeToRequestCancellation(Uri endpoint, Guid requestId, Func<Task> onRpcCancellation, CancellationToken cancellationToken)
        {
            return halibutRedisTransport.SubscribeToRequestCancellation(endpoint, requestId, onRpcCancellation, cancellationToken);
        }

        public Task PublishCancellation(Uri endpoint, Guid requestId, CancellationToken cancellationToken)
        {
            return halibutRedisTransport.PublishCancellation(endpoint, requestId, cancellationToken);
        }

        public Task MarkRequestAsCancelled(Uri endpoint, Guid requestId, TimeSpan ttl, CancellationToken cancellationToken)
        {
            return halibutRedisTransport.MarkRequestAsCancelled(endpoint, requestId, ttl, cancellationToken);
        }

        public Task<bool> IsRequestMarkedAsCancelled(Uri endpoint, Guid requestId, CancellationToken cancellationToken)
        {
            return halibutRedisTransport.IsRequestMarkedAsCancelled(endpoint, requestId, cancellationToken);
        }

        public Task<IAsyncDisposable> SubscribeToNodeHeartBeatChannel(Uri endpoint, Guid requestId, HalibutQueueNodeSendingPulses nodeSendingPulsesType, Func<Task> onHeartBeat, CancellationToken cancellationToken)
        {
            return halibutRedisTransport.SubscribeToNodeHeartBeatChannel(endpoint, requestId, nodeSendingPulsesType, onHeartBeat, cancellationToken);
        }

        public Task SendNodeHeartBeat(Uri endpoint, Guid requestId, HalibutQueueNodeSendingPulses nodeSendingPulsesType, CancellationToken cancellationToken)
        {
            return halibutRedisTransport.SendNodeHeartBeat(endpoint, requestId, nodeSendingPulsesType, cancellationToken);
        }

        public Task<IAsyncDisposable> SubscribeToResponseChannel(Uri endpoint, Guid identifier, Func<string, Task> onValueReceived, CancellationToken cancellationToken)
        {
            return halibutRedisTransport.SubscribeToResponseChannel(endpoint, identifier, onValueReceived, cancellationToken);
        }

        public Task PublishThatResponseIsAvailable(Uri endpoint, Guid identifier, CancellationToken cancellationToken)
        {
            return halibutRedisTransport.PublishThatResponseIsAvailable(endpoint, identifier, cancellationToken);
        }

        public Task SetResponseMessage(Uri endpoint, Guid identifier, string responseMessage, TimeSpan ttl, CancellationToken cancellationToken)
        {
            return halibutRedisTransport.SetResponseMessage(endpoint, identifier, responseMessage, ttl, cancellationToken);
        }

        public Task<string?> GetResponseMessage(Uri endpoint, Guid identifier, CancellationToken cancellationToken)
        {
            return halibutRedisTransport.GetResponseMessage(endpoint, identifier, cancellationToken);
        }

        public Task<bool> DeleteResponseMessage(Uri endpoint, Guid identifier, CancellationToken cancellationToken)
        {
            return halibutRedisTransport.DeleteResponseMessage(endpoint, identifier, cancellationToken);
        }
    }
}
#endif