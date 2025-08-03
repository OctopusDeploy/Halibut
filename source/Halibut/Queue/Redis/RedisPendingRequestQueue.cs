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
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.ServiceModel;
using Halibut.Transport.Protocol;
using Halibut.Util;
using Nito.AsyncEx;

namespace Halibut.Queue.Redis
{
    class RedisPendingRequestQueue : IPendingRequestQueue, IDisposable
    {
        readonly Uri endpoint;
        readonly ILog log;
        readonly HalibutRedisTransport halibutRedisTransport;
        readonly HalibutTimeoutsAndLimits halibutTimeoutsAndLimits;
        readonly MessageReaderWriter messageReaderWriter;
        readonly AsyncManualResetEvent hasItemsForEndpoint = new();

        readonly CancellationTokenSource queueCts = new ();
        ConcurrentDictionary<Guid, DisposableCollection> disposablesForInFlightRequests = new();
        readonly CancellationToken queueToken;
        
        Task<IAsyncDisposable> PulseChannelSubDisposer { get; }
        
        public RedisPendingRequestQueue(
            Uri endpoint, 
            ILog log, 
            HalibutRedisTransport halibutRedisTransport, 
            MessageReaderWriter messageReaderWriter, 
            HalibutTimeoutsAndLimits halibutTimeoutsAndLimits)
        {
            this.endpoint = endpoint;
            this.log = log;
            this.messageReaderWriter = messageReaderWriter;
            this.halibutRedisTransport = halibutRedisTransport;
            this.halibutTimeoutsAndLimits = halibutTimeoutsAndLimits;
            this.queueToken = queueCts.Token;
            
            // TODO: can we unsub if no tentacle is asking for a work for an extended period of time?
            // and also NOT sub if the queue is being created to send work. 
            // The advice is many channels with few subscribers is better than a single channel with many subscribers.
            // If we end up with too many channels, we could shared the channels based on modulo of the hash of the endpoint,
            // which means we might have only 1000 channels and num_tentacles/1000 subscribers to each channel. For 300K tentacles.
            PulseChannelSubDisposer = Task.Run(() => this.halibutRedisTransport.SubscribeToRequestMessagePulseChannel(endpoint, _ => hasItemsForEndpoint.Set(), queueToken));
        }
        
        public async ValueTask DisposeAsync()
        {
            await Try.IgnoringError(async () => await queueCts.CancelAsync());
            Try.IgnoringError(() => queueCts.Dispose());
            await Try.IgnoringError(async () => await (await PulseChannelSubDisposer).DisposeAsync());
        }

        public async Task<ResponseMessage> QueueAndWaitAsync(RequestMessage request, CancellationToken requestCancellationToken)
        {
            // TODO: redis goes down
            // TODO: Other node goes down.
            // TODO: Respect cancellation token
            // TODO RedisConnectionException can be raised out of here, what should the queue do?
            using var pending = new PendingRequest(request, log);
            
            // TODO: What if this payload was gigantic
            // TODO: Do we need to encrypt this?
            var payload = await messageReaderWriter.PrepareRequest(request, requestCancellationToken);
            
            // Start listening for a response to the request, we don't want to miss the response.
            await using var _ = await SubscribeToResponse(request.ActivityId, pending.SetResponse, requestCancellationToken);

            await using var tryClearRequestFromQueueWhenRequestIsCancelled 
                = pending.PendingRequestCancellationToken.Register(async () => await TryClearRequestFromQueue(request, pending));
            await using var trySendCancelWhenRequestIsCancelled
                = pending.PendingRequestCancellationToken.Register(async () => await WatchForRequestCancellation.TrySendCancellation(halibutRedisTransport, endpoint, request));

            // Make the request available before we tell people it is available.
            await halibutRedisTransport.PutRequest(endpoint, request.ActivityId, payload, requestCancellationToken);
            await halibutRedisTransport.PushRequestGuidOnToQueue(endpoint, request.ActivityId, requestCancellationToken);
            await halibutRedisTransport.PulseRequestPushedToEndpoint(endpoint, requestCancellationToken);
            
            await pending.WaitUntilComplete(() => TryClearRequestFromQueue(request, pending), requestCancellationToken);
            
            return pending.Response!;
        }

        async Task TryClearRequestFromQueue(RequestMessage request, PendingRequest pending)
        {
            // The time the message is allowed to sit on the queue for has elapsed.
            // Let's try to pop if from the queue, either:
            // - We pop it, which means it was never collected so let pending deal with the timeout.
            // - We could not pop it, which means it was collected.
            using var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMinutes(2)); // Best efforts.
            var requestJson = await halibutRedisTransport.TryGetAndRemoveRequest(endpoint, request.ActivityId, cts.Token);
            if (requestJson != null)
            {
                await pending.RequestHasBeenCollectedAndWillBeTransfered();
            }
        }

        

        async Task<IAsyncDisposable> SubscribeToResponse(Guid activityId,
            Action<ResponseMessage> onResponse,
            CancellationToken cancellationToken)
        {
            return await halibutRedisTransport.SubScribeToResponses(endpoint, activityId, async (responseJson) =>
            {
                var response = await messageReaderWriter.ReadResponse(responseJson, cancellationToken);
                onResponse(response);
            }, cancellationToken);
        }

        public bool IsEmpty => throw new NotImplementedException();
        public int Count => throw new NotImplementedException();

        

        public async Task<RequestMessageWithCancellationToken?> DequeueAsync(CancellationToken cancellationToken)
        {
            var pending = await DequeueNextAsync();
            if (pending == null) return null;

            var watchForRequestCancellation = new WatchForRequestCancellation(endpoint, pending.ActivityId, halibutRedisTransport);
            disposablesForInFlightRequests[pending.ActivityId] = new DisposableCollection(watchForRequestCancellation);
            
            
            return new RequestMessageWithCancellationToken(pending, watchForRequestCancellation.RequestCancelledCancellationToken);
        }

        
        
        public async Task ApplyResponse(ResponseMessage response, Guid requestActivityId)
        {
            if (response == null) return;

            var cancellationToken = CancellationToken.None;
            
            // This node has now completed the RPC, and so the response must be sent
            // back to the node which sent the response
            
            var payload = await messageReaderWriter.PrepareResponse(response, cancellationToken);
            await halibutRedisTransport.PublishResponse(endpoint, requestActivityId, payload, cancellationToken);
        }

        async Task<RequestMessage?> DequeueNextAsync()
        {
            var cancellationToken = CancellationToken.None;
            
            using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            try
            {
                // TODO can we avoid going to redis here?
                var first = await TryRemoveNextItemFromQueue(cancellationToken);
                if (first != null) return first;

                await Task.WhenAny(
                    hasItemsForEndpoint.WaitAsync(cancellationTokenSource.Token), 
                    Task.Delay(halibutTimeoutsAndLimits.PollingQueueWaitTimeout, cancellationTokenSource.Token));

                if (!hasItemsForEndpoint.IsSet)
                {
                    // Timed out waiting for something to go on the queue, send back a null to tentacle
                    // to keep the connection healthy.
                    return null;
                }

                // TODO: Does this work well for multiple clients? We might go round before we collect work.
                // TODO: test this.
                hasItemsForEndpoint.Reset();
                return await TryRemoveNextItemFromQueue(cancellationToken);
            }
            finally
            {
                await cancellationTokenSource.CancelAsync();
            }
        }

        async Task<RequestMessage?> TryRemoveNextItemFromQueue(CancellationToken cancellationToken)
        {
            while (true)
            {
                var activityId = await halibutRedisTransport.TryPopNextRequestGuid(endpoint, cancellationToken);

                if (activityId is null)
                {
                    // Nothing is on the queue.
                    return null;
                }
                
                var jsonRequest = await halibutRedisTransport.TryGetAndRemoveRequest(endpoint, activityId.Value, cancellationToken);

                if (jsonRequest == null)
                {
                    // This request has been picked up by someone else, go around the loop and look for something else to do.
                    continue;
                }

                var request = await messageReaderWriter.ReadRequest(jsonRequest, cancellationToken);

                return request;
            }
        }

        

        public void Dispose()
        {
            DisposeAsync().GetAwaiter().GetResult();
        }
    }
}