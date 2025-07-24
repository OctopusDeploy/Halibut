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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.ServiceModel;
using Halibut.Transport.Protocol;
using Nito.AsyncEx;

namespace Halibut.Queue.Redis
{
    class RedisPendingRequestQueue : IPendingRequestQueue, IDisposable
    {
        readonly public static TimeSpan PollingRequestMaximumMessageProcessingTimeout = TimeSpan.FromHours(33);
        
        readonly Dictionary<string, PendingRequest> inProgress = new();
        //TODO: Do we really need the lock (which came from original implementation)
        readonly object sync = new();
        readonly AsyncManualResetEvent hasItemsForEndpoint = new();
        readonly Uri endpoint;
        readonly ILog log;
        readonly IHalibutRedisTransport halibutRedisTransport;

        readonly QueueMessageSerializer queueMessageSerializer;
        readonly IStoreDataStreamsForDistributedQueues storeDataStreamsForDistributedQueues;

        public delegate RedisPendingRequestQueue Factory(Uri endpoint, ILog log);

        public RedisPendingRequestQueue(
            Uri endpoint, 
            ILog log, 
            IHalibutRedisTransport halibutRedisTransport, 
            QueueMessageSerializer queueMessageSerializer, 
            IStoreDataStreamsForDistributedQueues storeDataStreamsForDistributedQueues)
        {
            this.endpoint = endpoint;
            this.log = log;
            this.halibutRedisTransport = halibutRedisTransport;
            this.queueMessageSerializer = queueMessageSerializer;
            this.storeDataStreamsForDistributedQueues = storeDataStreamsForDistributedQueues;

            halibutRedisTransport.NewRequestEvent += HandleNewRequest;
            halibutRedisTransport.NewResponseEvent += HandleNewResponse;
            halibutRedisTransport.RequestPoppedEvent += HandleRequestPopped;
        }

        public void Dispose()
        {
            halibutRedisTransport.NewRequestEvent += HandleNewRequest;
            halibutRedisTransport.NewResponseEvent += HandleNewResponse;
        }

        void HandleNewRequest(object? sender, RedisHalibutQueueItem e)
        {
            lock (sync)
            {
                if (e.Endpoint == endpoint)
                {
                    hasItemsForEndpoint.Set();
                }
            }
        }

        void HandleNewResponse(object? sender, RedisHalibutQueueItem e)
        {
            lock (sync)
            {
                if (inProgress.TryGetValue(e.RequestId, out var pendingRequest))
                {
                    var responseMessage = halibutRedisTransport.GetDeleteResponse(e.RequestId).GetAwaiter().GetResult();
                    
                    var (response, dataStreams) = this.queueMessageSerializer.ReadMessage<ResponseMessage>(responseMessage!);
                    storeDataStreamsForDistributedQueues.ReHydrateDataStreams(dataStreams, CancellationToken.None).GetAwaiter().GetResult();

                    pendingRequest.SetResponse(response!);
                }
            }
        }

        void HandleRequestPopped(object? sender, RedisHalibutQueueItem e)
        {
            lock (sync)
            {
                if (inProgress.TryGetValue(e.RequestId, out var pendingRequest))
                {
                    pendingRequest.BeginTransfer();
                }
            }
        }

        public Task ApplyResponse(ResponseMessage response, ServiceEndPoint destination)
        {
            throw new NotImplementedException();
        }

        public async Task<ResponseMessage> QueueAndWaitAsync(RequestMessage request, CancellationToken cancellationToken)
        {
            var pending = new PendingRequest(request, log);

            lock (sync)
            {
                inProgress.Add(request.Id, pending);
            }

            var (payload, dataStreams) = queueMessageSerializer.WriteMessage(request);
            await storeDataStreamsForDistributedQueues.StoreDataStreams(dataStreams, cancellationToken);

            await halibutRedisTransport.PushRequest(endpoint, request.Id, payload);

            pending.WaitUntilComplete(cancellationToken);

            lock (sync)
            {
                inProgress.Remove(request.Id);
            }

            return pending.Response!;
        }

        public bool IsEmpty => throw new NotImplementedException();
        public int Count => throw new NotImplementedException();

        public async Task<RequestMessageWithCancellationToken?> DequeueAsync(CancellationToken cancellationToken)
        {
            var pending = await DequeueNextAsync();
            if (pending == null) return null;
            return new RequestMessageWithCancellationToken(pending, CancellationToken.None);
        }

        async Task<RequestMessage?> DequeueNextAsync()
        {
            var first = await TakeFirst();
            if (first != null) return first;

            await Task.WhenAny(hasItemsForEndpoint.WaitAsync(), Task.Delay(new HalibutTimeoutsAndLimits().PollingQueueWaitTimeout));
            hasItemsForEndpoint.Reset();
            return await TakeFirst();
        }

        async Task<RequestMessage?> TakeFirst()
        {
            var redisQueueItem = await halibutRedisTransport.PopRequest(endpoint);

            if (redisQueueItem is null) return null;

            var (request, dataStreams) = queueMessageSerializer.ReadMessage<RequestMessage>(redisQueueItem.PayloadJson);
            storeDataStreamsForDistributedQueues.ReHydrateDataStreams(dataStreams, CancellationToken.None).GetAwaiter().GetResult();

            return request;
        }

        public void ApplyResponse(ResponseMessage response)
        {
            if (response == null) return;

            lock (sync)
            {
                var (payload, dataStreams) = queueMessageSerializer.WriteMessage(response);
                storeDataStreamsForDistributedQueues.StoreDataStreams(dataStreams, CancellationToken.None).GetAwaiter().GetResult();

                halibutRedisTransport.SetResponse(endpoint, response.Id, payload).GetAwaiter().GetResult();
            }
        }


        class PendingRequest
        {
            readonly RequestMessage request;
            readonly ILog log;
            readonly ManualResetEventSlim waiter;
            readonly object sync = new object();
            bool transferBegun;
            bool completed;

            public PendingRequest(RequestMessage request, ILog log)
            {
                this.request = request;
                this.log = log;
                waiter = new ManualResetEventSlim(false);
            }


            public void WaitUntilComplete(CancellationToken cancellationToken)
            {
                log.Write(EventType.MessageExchange, "Request {0} was queued", request);

                var success = waiter.Wait(request.Destination.PollingRequestQueueTimeout, cancellationToken);
                if (success)
                {
                    log.Write(EventType.MessageExchange, "Request {0} was collected by the polling endpoint", request);
                    return;
                }

                var waitForTransferToComplete = false;
                lock (sync)
                {
                    if (transferBegun)
                    {
                        waitForTransferToComplete = true;
                    }
                    else
                    {
                        completed = true;
                    }
                }

                if (waitForTransferToComplete)
                {
                    success = waiter.Wait(PollingRequestMaximumMessageProcessingTimeout);
                    if (success)
                    {
                        log.Write(EventType.MessageExchange, "Request {0} was eventually collected by the polling endpoint", request);
                    }
                    else
                    {
                        SetResponse(ResponseMessage.FromException(request, new TimeoutException(string.Format("A request was sent to a polling endpoint, the polling endpoint collected it but did not respond in the allowed time ({0}), so the request timed out.", PollingRequestMaximumMessageProcessingTimeout))));
                    }
                }
                else
                {
                    log.Write(EventType.MessageExchange, "Request {0} timed out before it could be collected by the polling endpoint", request);
                    SetResponse(ResponseMessage.FromException(request, new TimeoutException(string.Format("A request was sent to a polling endpoint, but the polling endpoint did not collect the request within the allowed time ({0}), so the request timed out.", request.Destination.PollingRequestQueueTimeout))));
                }
            }

            public bool BeginTransfer()
            {
                lock (sync)
                {
                    if (completed)
                        return false;

                    transferBegun = true;
                    return true;
                }
            }

            public ResponseMessage? Response { get; private set; }

            public void SetResponse(ResponseMessage response)
            {
                Response = response;
                waiter.Set();
            }
        }

        public ValueTask DisposeAsync()
        {
            throw new NotImplementedException();
        }
    }

    interface IHalibutMessageQueueItemSerializer
    {
        T FromJson<T>(string responseMessage);
        string ToJson(object message);
    }

    public enum MessageType
    {
        RequestMessage,
        ResponseMessage
    }
}