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
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Halibut.Queue.Redis
{
    class HalibutHalibutRedisTransport : IHalibutRedisTransport
    {
        const string Namespace = "octopus:server:halibut";
        static readonly string RequestChannel = $"{Namespace}:request";
        static readonly string ResponseChannel = $"{Namespace}:response";
        static readonly string RequestPoppedChannel = $"{Namespace}:requestpopped";

        readonly IRedisConnection connection;
        
        //TODO: Rethink .net event
        public event EventHandler<RedisHalibutQueueItem>? NewRequestEvent;
        public event EventHandler<RedisHalibutQueueItem>? NewResponseEvent;
        public event EventHandler<RedisHalibutQueueItem>? RequestPoppedEvent;

        public HalibutHalibutRedisTransport(IRedisConnection connection)
        {
            this.connection = connection;

            SubscribeToChannels();
        }

        void SubscribeToChannels()
        {

            connection.SubscribeToChannel(
                RequestChannel,
                message =>
                {
                    
                    var queueItem = JsonConvert.DeserializeObject<RedisHalibutQueueItem>(message.Message!);
                    NewRequestEvent?.Invoke(this, queueItem!);
                });

            connection.SubscribeToChannel(
                ResponseChannel,
                message =>
                {
                    var queueItem = JsonConvert.DeserializeObject<RedisHalibutQueueItem>(message.Message!);
                    NewResponseEvent?.Invoke(this, queueItem!);
                });

            connection.SubscribeToChannel(
                RequestPoppedChannel,
                message =>
                {
                    var queueItem = JsonConvert.DeserializeObject<RedisHalibutQueueItem>(message.Message!);
                    RequestPoppedEvent?.Invoke(this, queueItem!);
                });
        }

        static string ResponseKey(string requestId)
        {
            var validRequestId = requestId.Replace(":", "_");
            return $"{Namespace}:response:{validRequestId}";
        }

        static string RequestKey(Uri endpoint)
        {
            return $"{Namespace}:request:{endpoint}";
        }

        public async Task SetResponse(Uri endpoint, string requestId, string payload)
        {
            var responseKey = ResponseKey(requestId);
            var responseExpiry = TimeSpan.FromMinutes(10);

            await connection.StringSet(responseKey, payload, responseExpiry);
            await PublishQueueItem(ResponseChannel, endpoint, requestId);
        }

        public async Task<string?> GetDeleteResponse(string requestId)
        {
            var responseKey = ResponseKey(requestId);
            var response = await connection.StringGetDelete(responseKey);
            return response;
        }

        public async Task PushRequest(Uri endpoint, string requestId, string payload)
        {
            var redisQueueItem = new RedisHalibuteQueueItem(requestId,payload);

            var serialisedQueueItem = JsonConvert.SerializeObject(redisQueueItem);

            var requestKey = RequestKey(endpoint);

            await connection.ListRightPush(requestKey, serialisedQueueItem);

            await PublishQueueItem(RequestChannel, endpoint, requestId);
        }
        
        public async Task<RedisHalibuteQueueItem?> PopRequest(Uri endpoint)
        {
            var requestKey = RequestKey(endpoint);

            var requestMessage = await connection.ListLeftPopAsync(requestKey);
            if (requestMessage is null) return null;

            var redisQueueItem = JsonConvert.DeserializeObject<RedisHalibuteQueueItem>(requestMessage);
            if (redisQueueItem is null) return null;

            await PublishQueueItem(RequestPoppedChannel, endpoint, redisQueueItem.RequestId);
            return redisQueueItem;
        }

        async Task PublishQueueItem(string channel, Uri endpoint, string requestId)
        {
            var queueItem = new RedisHalibutQueueItem(
                endpoint,
                requestId);

            await connection.PublishToChannel(channel, queueItem);
        }
    }

    interface IHalibutRedisTransport
    {
        event EventHandler<RedisHalibutQueueItem> NewRequestEvent;
        event EventHandler<RedisHalibutQueueItem> NewResponseEvent;
        event EventHandler<RedisHalibutQueueItem> RequestPoppedEvent;

        Task SetResponse(Uri endpoint, string requestId, string payload);
        Task PushRequest(Uri endpoint, string requestId, string payload);
        Task<string?> GetDeleteResponse(string requestId);
        Task<RedisHalibuteQueueItem?> PopRequest(Uri endpoint);
    }

    public class RedisHalibuteQueueItem
    {
        public RedisHalibuteQueueItem(string requestId, string payloadJson)
        {
            RequestId = requestId;
            PayloadJson = payloadJson;
        }

        public string RequestId { get; protected set; }
        public string PayloadJson { get; protected set; }
    }

    public class RedisHalibutQueueItem
    {
        public Uri Endpoint { get; protected set; }
        public string RequestId { get; protected set; }
        
        public RedisHalibutQueueItem(Uri endpoint, string requestId)
        {
            Endpoint = endpoint;
            RequestId = requestId;
        }
    }
}