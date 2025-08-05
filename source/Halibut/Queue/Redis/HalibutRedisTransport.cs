// Copyright 2012-2013 Octopus Deploy Pty. Ltd.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//   http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Util;
using Newtonsoft.Json;
using Nito.AsyncEx;
using StackExchange.Redis;

namespace Halibut.Queue.Redis
{
    public class HalibutRedisTransport
    {
        const string Namespace = "octopus:server:halibut";

        readonly RedisFacade facade;

        public HalibutRedisTransport(RedisFacade facade)
        {
            this.facade = facade;
        }

        // Request Pulse
        static string RequestMessagesPulseChannelName(Uri endpoint)
        {
            return $"{Namespace}::RequestMessagesPulseChannelName::{endpoint}";
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
            await facade.PublishToChannel(channelName, emptyJson);
        }

        // Request IDs list

        static string KeyForNextRequestGuidInListForEndpoint(Uri endpoint)
        {
            return $"{Namespace}::NextRequestInListForEndpoint::{endpoint}";
        }

        public async Task PushRequestGuidOnToQueue(Uri endpoint, Guid guid, CancellationToken cancellationToken)
        {
            // TODO should we overcomplicate this with json?
            // TODO TTL
            await facade.ListRightPushAsync(KeyForNextRequestGuidInListForEndpoint(endpoint), guid.ToString());
        }

        public async Task<Guid?> TryPopNextRequestGuid(Uri endpoint, CancellationToken cancellationToken)
        {
            var result = await facade.ListLeftPopAsync(KeyForNextRequestGuidInListForEndpoint(endpoint));
            return result.ToGuid();
        }

        // Request Message

        static string RequestMessageKey(Uri endpoint, Guid requestId)
        {
            return $"{Namespace}::RequestMessageKey::{endpoint}::{requestId}";
        }

        static string RequestField = "RequestField";

        public async Task PutRequest(Uri endpoint, Guid requestId, string payload, CancellationToken cancellationToken)
        {
            var redisQueueItem = new RedisHalibutQueueItem2(requestId, payload);

            var serialisedQueueItem = JsonConvert.SerializeObject(redisQueueItem);

            var requestKey = RequestMessageKey(endpoint, requestId);

            await facade.SetInHash(requestKey, RequestField, serialisedQueueItem);
        }

        public async Task<string?> TryGetAndRemoveRequest(Uri endpoint, Guid requestId, CancellationToken cancellationToken)
        {
            var requestKey = RequestMessageKey(endpoint, requestId);
            var requestMessage = await facade.TryGetAndDeleteFromHash(requestKey, RequestField);
            if (requestMessage == null) return null;

            var redisQueueItem = JsonConvert.DeserializeObject<RedisHalibutQueueItem2>(requestMessage);
            if (redisQueueItem is null) return null;

            return redisQueueItem.PayloadJson;
        }

        public async Task<bool> IsRequestStillOnQueue(Uri endpoint, Guid requestId, CancellationToken cancellationToken)
        {
            var requestKey = RequestMessageKey(endpoint, requestId);
            return await facade.HashContainsKey(requestKey, RequestField);
        }

        // Cancellation channel
        static string RequestCancelledChannel(Uri endpoint, Guid requestId)
        {
            return $"{Namespace}::RequestCancelledChannel::{endpoint}::{requestId}";
        }

        public async Task<IAsyncDisposable> SubscribeToRequestCancellation(Uri endpoint, Guid request,
            Func<Task> onCancellationReceived,
            CancellationToken cancellationToken)
        {
            var channelName = RequestCancelledChannel(endpoint, request);
            return await facade.SubscribeToChannel(channelName, async foo =>
            {
                string? response = foo.Message;
                if (response is not null) await onCancellationReceived();
            }, cancellationToken);
        }

        public async Task PublishCancellation(Uri endpoint, Guid requestId, CancellationToken cancellationToken)
        {
            var channelName = RequestCancelledChannel(endpoint, requestId);
            await facade.PublishToChannel(channelName, "{}");
        }

        // Cancellation Notification
        // TODO: I think this wants to become some sort of: is the sender still interested in the results
        // e.g. if the sender of the request is gone we should treat that as a cancellation.

        public string RequestCancelledMarkerKey(Uri endpoint, Guid requestId)
        {
            return $"{Namespace}::RequestCancelledMarkerKey::{endpoint}::{requestId}";
        }

        public async Task MarkRequestAsCancelled(Uri endpoint, Guid requestId, CancellationToken cancellationToken)
        {
            var key = RequestCancelledMarkerKey(endpoint, requestId);
            await facade.SetString(key, "{}");
        }
        
        public async Task<bool> IsRequestMarkedAsCancelled(Uri endpoint, Guid requestId, CancellationToken cancellationToken)
        {
            var key = RequestCancelledMarkerKey(endpoint, requestId);
            return (await facade.GetString(key)) != null;
        }
        
        
        // Node Processing the request heart beat channel
        static string NodeProcessingTheRequestHeartBeatChannel(Uri endpoint, Guid requestId)
        {
            return $"{Namespace}::NodeProcessingTheRequestHeartBeatChannel::{endpoint}::{requestId}";
        }

        public async Task<IAsyncDisposable> SubscribeToNodeProcessingTheRequestHeartBeatChannel(
            Uri endpoint, 
            Guid request,
            Func<Task> onHeartBeat,
            CancellationToken cancellationToken)
        {
            var channelName = NodeProcessingTheRequestHeartBeatChannel(endpoint, request);
            return await facade.SubscribeToChannel(channelName, async foo =>
            {
                string? response = foo.Message;
                if (response is not null) await onHeartBeat();
            }, cancellationToken);
        }

        public async Task SendHeartBeatFromNodeProcessingTheRequest(Uri endpoint, Guid requestId, CancellationToken cancellationToken)
        {
            var channelName = NodeProcessingTheRequestHeartBeatChannel(endpoint, requestId);
            await facade.PublishToChannel(channelName, "{}");
        }
        
        // Generic methods for watching for any string value being set
        
        string GenericChannelName(string thingToWatchFor, Uri endpoint, Guid identifier)
        {
            return $"{Namespace}::GenericChannel::{thingToWatchFor}::{endpoint}::{identifier}";
        }
        
        public async Task<IAsyncDisposable> SubscribeToGenericNotification(string thingToWatchFor, Uri endpoint, Guid identifier,
            Func<string, Task> onValueReceived,
            CancellationToken cancellationToken)
        {
            var channelName = GenericChannelName(thingToWatchFor, endpoint, identifier);
            return await facade.SubscribeToChannel(channelName, async foo =>
            {
                string? value = foo.Message;
                if (value is not null) await onValueReceived(value);
            }, cancellationToken);
        }
        
        public async Task PublishThatValueIsAvailable(string thingToWatchFor, Uri endpoint, Guid identifier, string value, CancellationToken cancellationToken)
        {
            var channelName = GenericChannelName(thingToWatchFor, endpoint, identifier);
            await facade.PublishToChannel(channelName, value);
        }
        
        string GenericMarkerKey(string thingToWatchFor, Uri endpoint, Guid identifier)
        {
            return $"{Namespace}::GenericMarker::{thingToWatchFor}::{endpoint}::{identifier}";
        }
        
        public async Task SendValue(string thingToWatchFor, Uri endpoint, Guid identifier, string value, CancellationToken cancellationToken)
        {
            // TODO: really must have a TTL
            var key = GenericMarkerKey(thingToWatchFor, endpoint, identifier);
            await facade.SetString(key, value);
        }
        
        public async Task<string?> GetGenericMarker(string thingToWatchFor, Uri endpoint, Guid identifier, CancellationToken cancellationToken)
        {
            var key = GenericMarkerKey(thingToWatchFor, endpoint, identifier);
            return await facade.GetString(key);
        }
        
        public async Task<bool> DeleteGenericMarker(string thingToWatchFor, Uri endpoint, Guid identifier, CancellationToken cancellationToken)
        {
            var key = GenericMarkerKey(thingToWatchFor, endpoint, identifier);
            return await facade.DeleteString(key);
        }
    }


    public class RedisHalibutQueueItem2
    {
        public RedisHalibutQueueItem2(Guid requestId, string payloadJson)
        {
            RequestId = requestId;
            PayloadJson = payloadJson;
        }

        public Guid RequestId { get; protected set; }
        public string PayloadJson { get; protected set; }
    }
}