
#if NET8_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Queue.Redis.MessageStorage;
using Halibut.Queue.Redis.NodeHeartBeat;
using Halibut.Util;
using StackExchange.Redis;

namespace Halibut.Queue.Redis.RedisHelpers
{
    public class HalibutRedisTransport : IHalibutRedisTransport
    {
        const string Namespace = "octopus::server::halibut";

        readonly RedisFacade facade;

        public HalibutRedisTransport(RedisFacade facade)
        {
            this.facade = facade;
        }
        
        // Request pulse channel.
        // Polling services will be notified of new request via this channel.
        // The Service will subscribe to the channel, while the client will publish (pulse)
        // the channel when a request is available.
        static string RequestMessagesPulseChannelName(Uri endpoint)
        {
            return $"{Namespace}::RequestMessagesPulseChannel::{endpoint}";
        }

        public async Task<IAsyncDisposable> SubscribeToRequestMessagePulseChannel(Uri endpoint, Action<ChannelMessage> onRequestMessagePulse, CancellationToken cancellationToken)
        {
            var channelName = RequestMessagesPulseChannelName(endpoint);
            return await facade.SubscribeToChannel(channelName, async message =>
            {
                await Task.CompletedTask;
                onRequestMessagePulse(message);
            },
                cancellationToken);
        }

        public async Task PulseRequestPushedToEndpoint(Uri endpoint, CancellationToken cancellationToken)
        {
            var channelName = RequestMessagesPulseChannelName(endpoint);
            string emptyJson = "{}"; // Maybe we will actually want to share data in the future, empty json means we can add stuff later.
            await facade.PublishToChannel(channelName, emptyJson, cancellationToken);
        }

        // Pending Request IDs list
        // A list in redis holding the set of available Pending Requests a Service can collect.
        // The Service will Pop the Ids while the Client will Push new Pending Request Ids to the list.

        static string PendingRequestGuidsQueueKey(Uri endpoint)
        {
            return $"{Namespace}::PendingRequestGuidsQueue::{endpoint}";
        }

        public async Task PushRequestGuidOnToQueue(Uri endpoint, Guid guid, CancellationToken cancellationToken)
        {
            // TTL is high since it applies to all GUIDs in the queue.
            var ttlForAllRequestsGuidsInList = TimeSpan.FromDays(1);
            await facade.ListRightPushAsync(PendingRequestGuidsQueueKey(endpoint), guid.ToString(), ttlForAllRequestsGuidsInList, cancellationToken);
        }

        public async Task<Guid?> TryPopNextRequestGuid(Uri endpoint, CancellationToken cancellationToken)
        {
            var result = await facade.ListLeftPopAsync(PendingRequestGuidsQueueKey(endpoint), cancellationToken);
            return result.ToGuid();
        }

        // Pending Request Message
        // Stores the Pending Request Message for collection by the service.
        // Note that the service will first need to TryPopNextRequestGuid to be able to
        // find the RequestMessage.

        static string RequestMessageKey(Uri endpoint, Guid requestId)
        {
            return $"{Namespace}::RequestMessage::{endpoint}::{requestId}";
        }
        
        /// <summary>
        /// The amount of time on top of the requestPickupTimout, the request will stay on the queue
        /// before being automatically picked up.
        /// The theory being we might need some grace period where it takes some time to collect
        /// the request. It is not clear if we need this. This will be addressed in:
        /// https://whimsical.com/under-some-circumstances-old-requests-can-still-be-sent-to-tenta-79CoT5PpvE1n5wApB6e2Zx
        /// </summary>
        static readonly TimeSpan AdditionalRequestMessageTtl = TimeSpan.FromMinutes(2);

        public async Task PutRequest(Uri endpoint, Guid requestId, RedisStoredMessage requestMessage, TimeSpan requestPickupTimeout, CancellationToken cancellationToken)
        {
            var requestKey = RequestMessageKey(endpoint, requestId);
            
            var ttl = requestPickupTimeout + AdditionalRequestMessageTtl;

            var dict = RedisStoredMessageToDictionary(requestMessage);

            await facade.SetInHash(requestKey, dict, ttl, cancellationToken);
        }

        /// <summary>
        /// Atomically Gets and removes the request from the queue.
        /// Exactly up to one caller of this method will be given the RequestMessage, all
        /// other calls will get null.
        /// Note: currently a minor issue exists where redis disconnecting mid "Delete" call
        /// can result in the Delete succeeding but no caller know if it succeeded. Thus,
        /// it might be possible that no one Gets the request. In this case normal heart beat
        /// timeouts will cause the request to be failed.
        /// </summary>
        /// <param name="endpoint"></param>
        /// <param name="requestId"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<RedisStoredMessage?> TryGetAndRemoveRequest(Uri endpoint, Guid requestId, CancellationToken cancellationToken)
        {
            var requestKey = RequestMessageKey(endpoint, requestId);
            var dict = await facade.TryGetAndDeleteFromHash(requestKey, RedisStoredMessageHashFields, cancellationToken);
            return DictionaryToRedisStoredMessage(dict);
        }

        public async Task<bool> IsRequestStillOnQueue(Uri endpoint, Guid requestId, CancellationToken cancellationToken)
        {
            var requestKey = RequestMessageKey(endpoint, requestId);
            return await facade.HashContainsKey(requestKey, RequestMessageField, cancellationToken);
        }

        // Cancellation channel
        // The node processing the request will subscribe to this channel, and the node
        // sending the request will publish to this channel when the RPC has been cancelled.
        static string RequestCancelledChannelName(Uri endpoint, Guid requestId)
        {
            return $"{Namespace}::RequestCancelledChannel::{endpoint}::{requestId}";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="endpoint"></param>
        /// <param name="requestId"></param>
        /// <param name="onRpcCancellation">Called when the RPC has been cancelled.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<IAsyncDisposable> SubscribeToRequestCancellation(Uri endpoint, Guid requestId, Func<Task> onRpcCancellation, CancellationToken cancellationToken)
        {
            var channelName = RequestCancelledChannelName(endpoint, requestId);
            return await facade.SubscribeToChannel(channelName, async foo =>
            {
                string? response = foo.Message;
                if (response is not null) await onRpcCancellation();
            }, cancellationToken);
        }

        public async Task PublishCancellation(Uri endpoint, Guid requestId, CancellationToken cancellationToken)
        {
            var channelName = RequestCancelledChannelName(endpoint, requestId);
            await facade.PublishToChannel(channelName, "{}", cancellationToken);
        }
        
        // Request cancellation
        // Since pub/sub does not have guaranteed delivery, cancellation can also
        // be detected by the RequestCancelledMarker. The node processing the request
        // will poll for the existence of the RequestCancelledMarker, and if found
        // it knows the RPC has been cancelled.
        public string RequestCancelledMarkerKey(Uri endpoint, Guid requestId)
        {
            return $"{Namespace}::RequestCancelledMarker::{endpoint}::{requestId}";
        }

        public async Task MarkRequestAsCancelled(Uri endpoint, Guid requestId, TimeSpan ttl, CancellationToken cancellationToken)
        {
            var key = RequestCancelledMarkerKey(endpoint, requestId);
            await facade.SetString(key, "{}", ttl, cancellationToken);
        }
        
        public async Task<bool> IsRequestMarkedAsCancelled(Uri endpoint, Guid requestId, CancellationToken cancellationToken)
        {
            var key = RequestCancelledMarkerKey(endpoint, requestId);
            return (await facade.GetString(key, cancellationToken)) != null;
        }
        
        // Node heartbeat channels (per request).
        // Each unique request has two node heart beat channels.
        // One channel for the `RequestSenderNode` where the node that executes the RPC,
        // publishes heart beats, for the duration of the time it is waiting for the RPC
        // to be executed.
        // Another channel for the `RequestProcessorNode` where the node that is sending the
        // request to the service (e.g. Tentacle) is publishing heart beats, for the duration
        // of processing the request.
        // Both nodes are able to monitor the heart beat channel of the other node to detect
        // if the other node has gone offline.
        
        static string NodeHeartBeatChannel(Uri endpoint, Guid requestId, HalibutQueueNodeSendingPulses nodeSendingPulsesType)
        {
            return $"{Namespace}::NodeHeartBeatChannel::{endpoint}::{requestId}::{nodeSendingPulsesType}";
        }

        public async Task<IAsyncDisposable> SubscribeToNodeHeartBeatChannel(
            Uri endpoint, 
            Guid requestId,
            HalibutQueueNodeSendingPulses nodeSendingPulsesType,
            Func<string, Task> onHeartBeat,
            CancellationToken cancellationToken)
        {
            var channelName = NodeHeartBeatChannel(endpoint, requestId, nodeSendingPulsesType);
            return await facade.SubscribeToChannel(channelName, async foo =>
            {
                string? response = foo.Message;
                if (response is not null) await onHeartBeat(response);
            }, cancellationToken);
        }

        public async Task SendNodeHeartBeat(Uri endpoint, Guid requestId, HalibutQueueNodeSendingPulses nodeSendingPulsesType, string nodeHeartBeatMessage, CancellationToken cancellationToken)
        {
            var channelName = NodeHeartBeatChannel(endpoint, requestId, nodeSendingPulsesType);
            await facade.PublishToChannel(channelName, nodeHeartBeatMessage, cancellationToken);
        }
        
        // Response channel.
        // The node processing the request `RequestProcessorNode` will publish to this channel
        // once the Response is available.
        
        static string ResponseChannelName(Uri endpoint, Guid identifier)
        {
            return $"{Namespace}::ResponseAvailableChannel::{endpoint}::{identifier}";
        }
        
        public async Task<IAsyncDisposable> SubscribeToResponseChannel(
            Uri endpoint,
            Guid identifier,
            Func<string, Task> onValueReceived,
            CancellationToken cancellationToken)
        {
            var channelName = ResponseChannelName(endpoint, identifier);
            return await facade.SubscribeToChannel(channelName, async foo =>
            {
                string? value = foo.Message;
                if (value is not null) await onValueReceived(value);
            }, cancellationToken);
        }
        
        public async Task PublishThatResponseIsAvailable(Uri endpoint, Guid identifier, CancellationToken cancellationToken)
        {
            var channelName = ResponseChannelName(endpoint, identifier);
            await facade.PublishToChannel(channelName, "{}", cancellationToken);
        }
        
        // Response 
        // This is where the Response is placed in Redis.
        
        static string ResponseMessageKey(Uri endpoint, Guid identifier)
        {
            return $"{Namespace}::Response::{endpoint}::{identifier}";
        }
        
        public async Task SetResponseMessage(Uri endpoint, Guid identifier, RedisStoredMessage responseMessage, TimeSpan ttl, CancellationToken cancellationToken)
        {
            var key = ResponseMessageKey(endpoint, identifier);
            var dict = RedisStoredMessageToDictionary(responseMessage);
            await facade.SetInHash(key, dict, ttl, cancellationToken);
        }
        
        public async Task<RedisStoredMessage?> GetResponseMessage(Uri endpoint, Guid identifier, CancellationToken cancellationToken)
        {
            var key = ResponseMessageKey(endpoint, identifier);
            var dict = await facade.TryGetFromHash(key, RedisStoredMessageHashFields, cancellationToken);
            return DictionaryToRedisStoredMessage(dict);
        }
        
        public async Task DeleteResponseMessage(Uri endpoint, Guid identifier, CancellationToken cancellationToken)
        {
            var key = ResponseMessageKey(endpoint, identifier);
            await facade.DeleteHash(key, cancellationToken);
        }
        
        static readonly string RequestMessageField = "RequestMessageField";
        static readonly string DataStreamMetaDataField = "DataStreamMetaDataField";
        static string[] RedisStoredMessageHashFields => new[] { RequestMessageField, DataStreamMetaDataField };
        
        static RedisStoredMessage? DictionaryToRedisStoredMessage(Dictionary<string, byte[]?>? dict)
        {
            if(dict == null) return null;
            var requestMessage = dict[RequestMessageField]!;
            
            // As it turns out Redis or our client seems to treat "" as null, which is insane
            // and results in us needing to deal with that here.
            var dataStreamMetadata = Array.Empty<byte>();
            if(dict.TryGetValue(DataStreamMetaDataField, out var dataStreamMetadataFromRedis))
            {
                dataStreamMetadata = dataStreamMetadataFromRedis ?? Array.Empty<byte>();
            }
            
            return new RedisStoredMessage(requestMessage, dataStreamMetadata);
        }
        
        static Dictionary<string, byte[]> RedisStoredMessageToDictionary(RedisStoredMessage requestMessage)
        {
            var dict = new Dictionary<string, byte[]>();
            dict[RequestMessageField] = requestMessage.Message;
            dict[DataStreamMetaDataField] = requestMessage.DataStreamMetadata;
            return dict;
        }
    }
}
#endif