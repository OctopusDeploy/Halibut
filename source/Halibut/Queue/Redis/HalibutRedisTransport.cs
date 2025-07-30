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

        public async Task<IAsyncDisposable> SubscribeToRequestMessagePulseChannel(Uri endpoint, Action<ChannelMessage> onRequestMessagePulse)
        {
            var channelName = RequestMessagesPulseChannelName(endpoint);
            return await facade.SubscribeToChannel(channelName, async message =>
            {
                await Task.CompletedTask;
                onRequestMessagePulse(message);
            });
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


        // Response channel
        static string ResponseMessagesChannelName(Uri endpoint, Guid requestId)
        {
            return $"{Namespace}::ResponseMessagesChannelName::{endpoint}::{requestId}";
        }

        public async Task<IAsyncDisposable> SubScribeToResponses(Uri endpoint, Guid requestOfResponseToWaitFor,
            Func<string, CancellationToken, Task> onResponse,
            CancellationToken cancellationToken)
        {
            var channelName = ResponseMessagesChannelName(endpoint, requestOfResponseToWaitFor);
            return await facade.SubscribeToChannel(channelName, async foo =>
            {
                string? response = foo.Message;
                if (response is not null) await onResponse(response, cancellationToken);
            });
        }

        public async Task PublishResponse(Uri endpoint, Guid requestId, string payload, CancellationToken cancellationToken)
        {
            var channelName = ResponseMessagesChannelName(endpoint, requestId);
            await facade.PublishToChannel(channelName, payload);
        }


        // Cancellation channel
        static string RequestCancelledChannel(Uri endpoint, Guid requestId)
        {
            return $"{Namespace}::RequestCancelledChannel::{endpoint}::{requestId}";
        }

        public async Task<IAsyncDisposable> SubscribeToRequestCancellation(Uri endpoint, Guid request,
            Func<CancellationToken, Task> onCancellationReceived,
            CancellationToken cancellationToken)
        {
            var channelName = RequestCancelledChannel(endpoint, request);
            return await facade.SubscribeToChannel(channelName, async foo =>
            {
                string? response = foo.Message;
                if (response is not null) await onCancellationReceived(cancellationToken);
            });
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