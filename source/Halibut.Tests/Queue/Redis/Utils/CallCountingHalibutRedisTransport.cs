#if NET8_0_OR_GREATER
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Queue.Redis.MessageStorage;
using Halibut.Queue.Redis.NodeHeartBeat;
using Halibut.Queue.Redis.RedisHelpers;
using StackExchange.Redis;

namespace Halibut.Tests.Queue.Redis.Utils
{
    public class CallCountingHalibutRedisTransport : IHalibutRedisTransport
    {
        readonly IHalibutRedisTransport inner;
        readonly ConcurrentDictionary<string, int> callCounts = new();

        public CallCountingHalibutRedisTransport(IHalibutRedisTransport inner)
        {
            this.inner = inner;
        }
        
        object mutex = new object();

        public int GetCallCount(string methodName)
        {
            lock (mutex)
            {
                return callCounts.GetOrAdd(methodName, 0);
            }
        }

        void IncrementCallCount(string methodName)
        {
            lock (mutex)
            {
                callCounts.AddOrUpdate(methodName, 1, (_, count) => count + 1);
            }
        }

        public Task<IAsyncDisposable> SubscribeToRequestMessagePulseChannel(Uri endpoint, Action<ChannelMessage> onRequestMessagePulse, CancellationToken cancellationToken)
        {
            IncrementCallCount(nameof(SubscribeToRequestMessagePulseChannel));
            return inner.SubscribeToRequestMessagePulseChannel(endpoint, onRequestMessagePulse, cancellationToken);
        }

        public Task PulseRequestPushedToEndpoint(Uri endpoint, CancellationToken cancellationToken)
        {
            IncrementCallCount(nameof(PulseRequestPushedToEndpoint));
            return inner.PulseRequestPushedToEndpoint(endpoint, cancellationToken);
        }

        public Task PushRequestGuidOnToQueue(Uri endpoint, Guid guid, CancellationToken cancellationToken)
        {
            IncrementCallCount(nameof(PushRequestGuidOnToQueue));
            return inner.PushRequestGuidOnToQueue(endpoint, guid, cancellationToken);
        }

        public Task<Guid?> TryPopNextRequestGuid(Uri endpoint, CancellationToken cancellationToken)
        {
            IncrementCallCount(nameof(TryPopNextRequestGuid));
            return inner.TryPopNextRequestGuid(endpoint, cancellationToken);
        }

        public Task PutRequest(Uri endpoint, Guid requestId, RedisStoredMessage requestMessage, TimeSpan requestPickupTimeout, CancellationToken cancellationToken)
        {
            IncrementCallCount(nameof(PutRequest));
            return inner.PutRequest(endpoint, requestId, requestMessage, requestPickupTimeout, cancellationToken);
        }

        public Task<RedisStoredMessage?> TryGetAndRemoveRequest(Uri endpoint, Guid requestId, CancellationToken cancellationToken)
        {
            IncrementCallCount(nameof(TryGetAndRemoveRequest));
            return inner.TryGetAndRemoveRequest(endpoint, requestId, cancellationToken);
        }

        public Task<bool> IsRequestStillOnQueue(Uri endpoint, Guid requestId, CancellationToken cancellationToken)
        {
            IncrementCallCount(nameof(IsRequestStillOnQueue));
            return inner.IsRequestStillOnQueue(endpoint, requestId, cancellationToken);
        }

        public Task<IAsyncDisposable> SubscribeToRequestCancellation(Uri endpoint, Guid requestId, Func<Task> onRpcCancellation, CancellationToken cancellationToken)
        {
            IncrementCallCount(nameof(SubscribeToRequestCancellation));
            return inner.SubscribeToRequestCancellation(endpoint, requestId, onRpcCancellation, cancellationToken);
        }

        public Task PublishCancellation(Uri endpoint, Guid requestId, CancellationToken cancellationToken)
        {
            IncrementCallCount(nameof(PublishCancellation));
            return inner.PublishCancellation(endpoint, requestId, cancellationToken);
        }

        public Task MarkRequestAsCancelled(Uri endpoint, Guid requestId, TimeSpan ttl, CancellationToken cancellationToken)
        {
            IncrementCallCount(nameof(MarkRequestAsCancelled));
            return inner.MarkRequestAsCancelled(endpoint, requestId, ttl, cancellationToken);
        }

        public Task<bool> IsRequestMarkedAsCancelled(Uri endpoint, Guid requestId, CancellationToken cancellationToken)
        {
            IncrementCallCount(nameof(IsRequestMarkedAsCancelled));
            return inner.IsRequestMarkedAsCancelled(endpoint, requestId, cancellationToken);
        }

        public Task<IAsyncDisposable> SubscribeToNodeHeartBeatChannel(Uri endpoint, Guid requestId, HalibutQueueNodeSendingPulses nodeSendingPulsesType, Func<string, Task> onHeartBeat, CancellationToken cancellationToken)
        {
            IncrementCallCount(nameof(SubscribeToNodeHeartBeatChannel));
            return inner.SubscribeToNodeHeartBeatChannel(endpoint, requestId, nodeSendingPulsesType, onHeartBeat, cancellationToken);
        }

        public Task SendNodeHeartBeat(Uri endpoint, Guid requestId, HalibutQueueNodeSendingPulses nodeSendingPulsesType, string nodeHeartBeatMessage, CancellationToken cancellationToken)
        {
            IncrementCallCount(nameof(SendNodeHeartBeat));
            return inner.SendNodeHeartBeat(endpoint, requestId, nodeSendingPulsesType, nodeHeartBeatMessage, cancellationToken);
        }

        public Task<IAsyncDisposable> SubscribeToResponseChannel(Uri endpoint, Guid identifier, Func<string, Task> onValueReceived, CancellationToken cancellationToken)
        {
            IncrementCallCount(nameof(SubscribeToResponseChannel));
            return inner.SubscribeToResponseChannel(endpoint, identifier, onValueReceived, cancellationToken);
        }

        public Task PublishThatResponseIsAvailable(Uri endpoint, Guid identifier, CancellationToken cancellationToken)
        {
            IncrementCallCount(nameof(PublishThatResponseIsAvailable));
            return inner.PublishThatResponseIsAvailable(endpoint, identifier, cancellationToken);
        }

        public Task SetResponseMessage(Uri endpoint, Guid identifier, RedisStoredMessage responseMessage, TimeSpan ttl, CancellationToken cancellationToken)
        {
            IncrementCallCount(nameof(SetResponseMessage));
            return inner.SetResponseMessage(endpoint, identifier, responseMessage, ttl, cancellationToken);
        }

        public Task<RedisStoredMessage?> GetResponseMessage(Uri endpoint, Guid identifier, CancellationToken cancellationToken)
        {
            IncrementCallCount(nameof(GetResponseMessage));
            return inner.GetResponseMessage(endpoint, identifier, cancellationToken);
        }

        public Task DeleteResponseMessage(Uri endpoint, Guid identifier, CancellationToken cancellationToken)
        {
            IncrementCallCount(nameof(DeleteResponseMessage));
            return inner.DeleteResponseMessage(endpoint, identifier, cancellationToken);
        }
    }
}
#endif

