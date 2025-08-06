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
        internal ConcurrentDictionary<Guid, DisposableCollection> disposablesForInFlightRequests = new();
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
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(queueCts.Token, requestCancellationToken);
            var cancellationToken = cts.Token;
            // TODO: redis goes down
            // TODO RedisConnectionException can be raised out of here, what should the queue do?
            using var pending = new PendingRequest(request, log);
            
            // TODO: What if this payload was gigantic
            // TODO: Do we need to encrypt this?
            var payload = await messageReaderWriter.PrepareRequest(request, cancellationToken);
            
            // Start listening for a response to the request, we don't want to miss the response.
            await using var _ = await SubscribeToResponse(request.ActivityId, pending.SetResponse, cancellationToken);

            var tryClearRequestFromQueueAtMostOnce = new AsyncLazy<bool>(async () => await TryClearRequestFromQueue(request, pending));
            try
            {
                await using var senderPulse = new NodeHeartBeatSender(endpoint, request.ActivityId, halibutRedisTransport, log, HalibutQueueNodeSendingPulses.Sender, DelayBetweenHeartBeatsForRequestSender);
                // Make the request available before we tell people it is available.
                await halibutRedisTransport.PutRequest(endpoint, request.ActivityId, payload, cancellationToken);
                await halibutRedisTransport.PushRequestGuidOnToQueue(endpoint, request.ActivityId, cancellationToken);
                await halibutRedisTransport.PulseRequestPushedToEndpoint(endpoint, cancellationToken);

                await using var watcherCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token).CancelOnDispose();
                WatchProcessingNodeIsStillConnectedInBackground(request, pending, watcherCts);

                await pending.WaitUntilComplete(async () => await tryClearRequestFromQueueAtMostOnce.Task, cancellationToken);
            }
            finally
            {
                // Make an attempt to ensure the request is removed from redis.
                var background = Task.Run(async () => await Try.IgnoringError(async () => await tryClearRequestFromQueueAtMostOnce.Task));
                var backgroundCancellation = Task.Run(async () => await SendCancellationIfRequestWasCancelled(request, pending));
            }

            return pending.Response!;
        }

        async Task SendCancellationIfRequestWasCancelled(RequestMessage request, PendingRequest pending)
        {
            if (pending.PendingRequestCancellationToken.IsCancellationRequested)
            {
                // TODO log
                await WatchForRequestCancellation.TrySendCancellation(halibutRedisTransport, endpoint, request, log);
            }
            else
            {
                // TODO log
            }
        }

        void WatchProcessingNodeIsStillConnectedInBackground(RequestMessage request, PendingRequest pending, CancelOnDisposeCancellationTokenSource watcherCts)
        {
            Task.Run(async () =>
            {
                var watcherCtsCancellationToken = watcherCts.CancellationToken;
                try
                {
                    var disconnected = await NodeHeartBeatSender.WatchThatNodeProcessingTheRequestIsStillAlive(endpoint, request, pending, halibutRedisTransport, log, NodeIsOfflineHeartBeatTimeoutForRequestProcessor, watcherCtsCancellationToken);
                    if (!watcherCtsCancellationToken.IsCancellationRequested && disconnected == NodeHeartBeatSender.NodeProcessingRequestWatcherResult.NodeMayHaveDisconnected)
                    {
                        // TODO: if(responseWatcher.CheckForResponseNow() == ResponseNotFound) {
                        pending.SetResponse(ResponseMessage.FromError(request, "The node processing the request did not send a heartbeat for long enough, and so the node is now assumed to be offline."));
                        //}
                    }
                }
                catch (Exception) when (watcherCtsCancellationToken.IsCancellationRequested)
                {
                    log.Write(EventType.Diagnostic, "Processing node watcher cancelled for request {0}, endpoint {1}", request.ActivityId, endpoint);
                }
                catch (Exception ex)
                {
                    log.WriteException(EventType.Error, "Error watching processing node for request {0}, endpoint {1}", ex, request.ActivityId, endpoint);
                }
            });
        }

        async Task<bool> TryClearRequestFromQueue(RequestMessage request, PendingRequest pending)
        { 
            log.Write(EventType.Diagnostic, "Attempting to clear request {0} from queue for endpoint {1}", request.ActivityId, endpoint);
            
            // The time the message is allowed to sit on the queue for has elapsed.
            // Let's try to pop if from the queue, either:
            // - We pop it, which means it was never collected so let pending deal with the timeout.
            // - We could not pop it, which means it was collected.
            try
            {
                if (pending.HasRequestBeenMarkedAsCollected)
                {
                    // TODO: log
                    return false;
                }
                using var cts = new CancellationTokenSource();
                cts.CancelAfter(TimeSpan.FromMinutes(2)); // Best efforts.
                var requestJson = await halibutRedisTransport.TryGetAndRemoveRequest(endpoint, request.ActivityId, cts.Token);
                if (requestJson != null)
                {
                    log.Write(EventType.Diagnostic, "Successfully removed request {0} from queue - request was never collected by a processing node", request.ActivityId);
                    return true;
                }
                else
                {
                    await pending.RequestHasBeenCollectedAndWillBeTransferred();
                    log.Write(EventType.Diagnostic, "Request {0} was not found in queue - it was already collected by a processing node", request.ActivityId);
                }
            }
            catch (Exception ex)
            {
                log.WriteException(EventType.Error, "Failed to clear request {0} from queue for endpoint {1}", ex, request.ActivityId, endpoint);
            }
            return false;
        }

        

        const string ResponseMessageSubscriptionName = "ResponseMessage";
        
        async Task<IAsyncDisposable> SubscribeToResponse(Guid activityId,
            Action<ResponseMessage> onResponse,
            CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            var sub = new PollAndSubscribeForSingleMessage(ResponseMessageSubscriptionName, endpoint, activityId, halibutRedisTransport, log);
            var _ = Task.Run(async () =>
            {
                try
                {
                    log.Write(EventType.Diagnostic, "Waiting for response for request {0}", activityId);
                    var responseJson = await sub.ResultTask;
                    log.Write(EventType.Diagnostic, "Received response JSON for request {0}, deserializing", activityId);
                    var response = await messageReaderWriter.ReadResponse(responseJson, cancellationToken);
                    log.Write(EventType.Diagnostic, "Successfully deserialized response for request {0}, invoking callback", activityId);
                    onResponse(response);
                }
                catch (OperationCanceledException)
                {
                    log.Write(EventType.Diagnostic, "Response subscription cancelled for request {0}", activityId);
                }
                catch (Exception ex)
                {
                    log.WriteException(EventType.Error, "Error while processing response for request {0}", ex, activityId);
                }
            });
            return sub;
        }

        public bool IsEmpty => Count == 0;
        public int Count => throw new NotImplementedException();
        

        // The timespan is more generous for the sender going offline, since if it does go offline,
        // since under some cases the request completing is advantageous. That node needs to
        // re-do the entire RPC for idempotent RPCs this might mean that the task required is already done.
        internal TimeSpan NodeIsOfflineHeartBeatTimeoutForRequestSender { get; set; }  = TimeSpan.FromSeconds(90);
        
        internal TimeSpan DelayBetweenHeartBeatsForRequestSender { get; set; }  = TimeSpan.FromSeconds(15);
        
        // Setting this too high means things above the RPC might not have time to retry.
        public TimeSpan NodeIsOfflineHeartBeatTimeoutForRequestProcessor { get; set; } = TimeSpan.FromSeconds(60);
        
        internal TimeSpan DelayBetweenHeartBeatsForRequestProcessor { get; set; }  = TimeSpan.FromSeconds(15);
        
        internal TimeSpan TTLOfResponseMessage { get; set; } = TimeSpan.FromMinutes(5);
        
        
        
        public async Task<RequestMessageWithCancellationToken?> DequeueAsync(CancellationToken cancellationToken)
        {
            // TODO: is it god or bad that redis exceptions will bubble out of here.
            // I think it will kill the TCP connection, which will force re-connect (in perhaps a backoff function)
            // This could result in connecting to a node that is actually connected to redis. It could also
            // cause a cascade of failure from high load.
            var pending = await DequeueNextAsync();
            if (pending == null) return null;

            
            var disposables = new DisposableCollection();
            try
            {
                disposables.AddAsyncDisposable(new NodeHeartBeatSender(endpoint, pending.ActivityId, halibutRedisTransport, log, HalibutQueueNodeSendingPulses.Receiver, DelayBetweenHeartBeatsForRequestProcessor));

                var watcher = new WatchForRequestCancellationOrSenderDisconnect(endpoint, pending.ActivityId, halibutRedisTransport, NodeIsOfflineHeartBeatTimeoutForRequestSender, log);
                var response = new RequestMessageWithCancellationToken(pending, watcher.RequestProcessingCancellationToken);
                disposablesForInFlightRequests[pending.ActivityId] = disposables;
                return response;
            }
            catch (Exception)
            {
                await Try.IgnoringError(async () => await disposables.DisposeAsync());
                throw;
            }
        }

        public async Task ApplyResponse(ResponseMessage response, Guid requestActivityId)
        {
            log.Write(EventType.MessageExchange, "Applying response for request {0}", requestActivityId);
            
            try
            {
                if (response == null) 
                {
                    log.Write(EventType.Diagnostic, "Response is null for request {0}, skipping apply", requestActivityId);
                    return;
                }

                log.Write(EventType.MessageExchange, "Preparing response payload for request {0}", requestActivityId);
                var cancellationToken = CancellationToken.None;

                // This node has now completed the RPC, and so the response must be sent
                // back to the node which sent the response

                var payload = await messageReaderWriter.PrepareResponse(response, cancellationToken);
                log.Write(EventType.MessageExchange, "Sending response message for request {0}", requestActivityId);
                await PollAndSubscribeForSingleMessage.TrySendMessage(ResponseMessageSubscriptionName, halibutRedisTransport, endpoint, requestActivityId, payload, TTLOfResponseMessage, log);
                log.Write(EventType.MessageExchange, "Successfully applied response for request {0}", requestActivityId);
            }
            catch (Exception ex)
            {
                log.WriteException(EventType.Error, "Error applying response for request {0}", ex, requestActivityId);
                throw;
            }
            finally
            {
                if (disposablesForInFlightRequests.TryRemove(requestActivityId, out var disposables))
                {
                    log.Write(EventType.Diagnostic, "Disposing in-flight request resources for request {0}", requestActivityId);
                    try
                    {
                        await disposables.DisposeAsync();
                        log.Write(EventType.Diagnostic, "Successfully disposed in-flight request resources for request {0}", requestActivityId);
                    }
                    catch (Exception ex)
                    {
                        log.WriteException(EventType.Diagnostic, "Error disposing in-flight request resources for request {0}", ex, requestActivityId);
                    }
                }
                else
                {
                    log.Write(EventType.Diagnostic, "No in-flight request resources found to dispose for request {0}", requestActivityId);
                }
            }
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
                log.Write(EventType.Diagnostic, "Successfully collected request {0} from queue for endpoint {1}", request.ActivityId, endpoint);

                return request;
            }
        }

        

        public void Dispose()
        {
            DisposeAsync().GetAwaiter().GetResult();
        }
    }
}