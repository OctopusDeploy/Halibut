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
        readonly IWatchForRedisLosingAllItsData watchForRedisLosingAllItsData;
        readonly ILog log;
        readonly HalibutRedisTransport halibutRedisTransport;
        readonly HalibutTimeoutsAndLimits halibutTimeoutsAndLimits;
        readonly MessageReaderWriter messageReaderWriter;
        readonly AsyncManualResetEvent hasItemsForEndpoint = new();

        readonly CancelOnDisposeCancellationToken queueCts = new ();
        internal ConcurrentDictionary<Guid, WatcherAndDisposables> disposablesForInFlightRequests = new();
        
        // TODO: this needs to be used in all public methods.
        readonly CancellationToken queueToken;
        
        int numberOfInFlightRequestsThatHaveReachedTheStageOfBeingReadyForCollection = 0;

        Task<IAsyncDisposable> PulseChannelSubDisposer { get; }
        
        public RedisPendingRequestQueue(
            Uri endpoint, 
            IWatchForRedisLosingAllItsData watchForRedisLosingAllItsData,
            ILog log, 
            HalibutRedisTransport halibutRedisTransport, 
            MessageReaderWriter messageReaderWriter, 
            HalibutTimeoutsAndLimits halibutTimeoutsAndLimits)
        {
            this.endpoint = endpoint;
            this.watchForRedisLosingAllItsData = watchForRedisLosingAllItsData;
            this.log = log.ForContext<RedisPendingRequestQueue>();
            this.messageReaderWriter = messageReaderWriter;
            this.halibutRedisTransport = halibutRedisTransport;
            this.halibutTimeoutsAndLimits = halibutTimeoutsAndLimits;
            this.queueToken = queueCts.Token;
            
            // TODO: can we unsub if no tentacle is asking for a work for an extended period of time?
            // and also NOT sub if the queue is being created to send work. 
            // The advice is many channels with few subscribers is better than a single channel with many subscribers.
            // If we end up with too many channels, we could shared the channels based on modulo of the hash of the endpoint,
            // which means we might have only 1000 channels and num_tentacles/1000 subscribers to each channel. For 300K tentacles.
            PulseChannelSubDisposer = Task.Run(async () => await this.halibutRedisTransport.SubscribeToRequestMessagePulseChannel(endpoint, _ => hasItemsForEndpoint.Set(), queueToken));
        }

        internal async Task WaitUntilQueueIsSubscribedToReceiveMessages() => await PulseChannelSubDisposer;
        
        public async ValueTask DisposeAsync()
        {
            await Try.IgnoringError(async () => await queueCts.DisposeAsync());
            await Try.IgnoringError(async () => await (await PulseChannelSubDisposer).DisposeAsync());
        }

        private async Task<CancellationToken> DataLossCancellationToken(CancellationToken? cancellationToken)
        {
            // TODO this must throw something that can be retried.
            await using var cts = new CancelOnDisposeCancellationToken(queueCts.Token, cancellationToken ?? CancellationToken.None);
            return await watchForRedisLosingAllItsData.GetTokenForDataLoseDetection(TimeSpan.FromSeconds(30), cts.Token);
        }

        public async Task<ResponseMessage> QueueAndWaitAsync(RequestMessage request, CancellationToken requestCancellationToken)
        {
            
            var dataLoseCt = await DataLossCancellationToken(requestCancellationToken);
            
            await using var cts = new CancelOnDisposeCancellationToken(queueCts.Token, requestCancellationToken, dataLoseCt);
            
            var cancellationToken = cts.Token;
            // TODO RedisConnectionException can be raised out of here, what should the queue do?
            // TODO it must raise an exception that supports being retried.
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
                Interlocked.Increment(ref numberOfInFlightRequestsThatHaveReachedTheStageOfBeingReadyForCollection);
                try
                {

                    await using var watcherCts = new CancelOnDisposeCancellationToken(cts.Token);
                    WatchProcessingNodeIsStillConnectedInBackground(request, pending, watcherCts);

                    // TODO: We need to ensure that no matter what exceptions are thrown we eventually exit.
                    // For example can the subscription to the response, fail and never come back?
                    // Can the WatchProcessProcessingNodeIsStillConnected fail and never come back?
                    await pending.WaitUntilComplete(
                        async () => await tryClearRequestFromQueueAtMostOnce.Task,
                        () => dataLoseCt.IsCancellationRequested ? 
                            new RedisDataLoseHalibutClientException($"Request {request.ActivityId} was cancelled because we detected that redis lost all of its data.") 
                            : null,
                        cancellationToken);
                }
                finally
                {
                    Interlocked.Decrement(ref numberOfInFlightRequestsThatHaveReachedTheStageOfBeingReadyForCollection);
                }
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

        void WatchProcessingNodeIsStillConnectedInBackground(RequestMessage request, PendingRequest pending, CancelOnDisposeCancellationToken watcherCts)
        {
            Task.Run(async () =>
            {
                var watcherCtsCancellationToken = watcherCts.Token;
                try
                {
                    var disconnected = await NodeHeartBeatSender.WatchThatNodeProcessingTheRequestIsStillAlive(
                        endpoint,
                        request,
                        pending,
                        halibutRedisTransport,
                        TimeBetweenCheckingIfRequestWasCollected,
                        log,
                        NodeIsOfflineHeartBeatTimeoutForRequestProcessor,
                        watcherCtsCancellationToken);
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
                await using var cts = new CancelOnDisposeCancellationToken();
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
            var sub = new PollAndSubscribeToResponse(ResponseMessageSubscriptionName, endpoint, activityId, halibutRedisTransport, log);
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
        public int Count => numberOfInFlightRequestsThatHaveReachedTheStageOfBeingReadyForCollection;

        // The timespan is more generous for the sender going offline, since if it does go offline,
        // since under some cases the request completing is advantageous. That node needs to
        // re-do the entire RPC for idempotent RPCs this might mean that the task required is already done.
        internal TimeSpan NodeIsOfflineHeartBeatTimeoutForRequestSender { get; set; }  = TimeSpan.FromSeconds(90);
        
        internal TimeSpan DelayBetweenHeartBeatsForRequestSender { get; set; }  = TimeSpan.FromSeconds(15);
        
        // Setting this too high means things above the RPC might not have time to retry.
        public TimeSpan NodeIsOfflineHeartBeatTimeoutForRequestProcessor { get; set; } = TimeSpan.FromSeconds(60);
        
        internal TimeSpan DelayBetweenHeartBeatsForRequestProcessor { get; set; }  = TimeSpan.FromSeconds(15);
        
        internal TimeSpan TTLOfResponseMessage { get; set; } = TimeSpan.FromMinutes(5);
        
        internal TimeSpan TimeBetweenCheckingIfRequestWasCollected { get; set; } = TimeSpan.FromSeconds(30);
        
        
        
        public async Task<RequestMessageWithCancellationToken?> DequeueAsync(CancellationToken cancellationToken)
        {
            // TODO: is it good or bad that redis exceptions will bubble out of here.
            // I think it will kill the TCP connection, which will force re-connect (in perhaps a backoff function)
            // This could result in connecting to a node that is actually connected to redis. It could also
            // cause a cascade of failure from high load.
            var pending = await DequeueNextAsync();
            if (pending == null) return null;

            
            var disposables = new DisposableCollection();
            try
            {
                // There is a chance the data loss occured after we got the data but before here.
                // In that case we will just time out because of the lack of heart beats.
                var dataLossCT = await this.watchForRedisLosingAllItsData.GetTokenForDataLoseDetection(TimeSpan.FromSeconds(30), queueToken);
                
                disposables.AddAsyncDisposable(new NodeHeartBeatSender(endpoint, pending.ActivityId, halibutRedisTransport, log, HalibutQueueNodeSendingPulses.Receiver, DelayBetweenHeartBeatsForRequestProcessor));
                var watcher = new WatchForRequestCancellationOrSenderDisconnect(endpoint, pending.ActivityId, halibutRedisTransport, NodeIsOfflineHeartBeatTimeoutForRequestSender, log);
                disposables.AddAsyncDisposable(watcher);
                
                var cts = new CancelOnDisposeCancellationToken(watcher.RequestProcessingCancellationToken, dataLossCT);
                disposables.AddAsyncDisposable(cts);
                
                var response = new RequestMessageWithCancellationToken(pending, cts.Token);
                disposablesForInFlightRequests[pending.ActivityId] = new WatcherAndDisposables(disposables, cts.Token, watcher);
                return response;
            }
            catch (Exception)
            {
                await Try.IgnoringError(async () => await disposables.DisposeAsync());
                throw;
            }
        }

        public class WatcherAndDisposables : IAsyncDisposable
        {
            readonly DisposableCollection disposableCollection;
            public CancellationToken RequestCancelledForAnyReasonCancellationToken { get; }
            public WatchForRequestCancellationOrSenderDisconnect watcher { get; }

            public WatcherAndDisposables(DisposableCollection disposableCollection, CancellationToken requestCancelledForAnyReasonCancellationToken, WatchForRequestCancellationOrSenderDisconnect watcher)
            {
                this.disposableCollection = disposableCollection;
                this.RequestCancelledForAnyReasonCancellationToken = requestCancelledForAnyReasonCancellationToken;
                this.watcher = watcher;
            }

            public async ValueTask DisposeAsync()
            {
                await Try.IgnoringError(async () => await disposableCollection.DisposeAsync());
            }
        }

        public const string RequestAbandonedMessage = "The request was abandoned, possibly because the node processing the request shutdown or redis lost all of its data.";
        public async Task ApplyResponse(ResponseMessage response, Guid requestActivityId)
        {
            log.Write(EventType.MessageExchange, "Applying response for request {0}", requestActivityId);
            WatcherAndDisposables? watcherAndDisposables = null;
            if (!disposablesForInFlightRequests.TryRemove(requestActivityId, out watcherAndDisposables))
            {
                log.Write(EventType.Diagnostic, "No in-flight request resources found to dispose for request {0}", requestActivityId);
            }
            
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

                if (watcherAndDisposables != null && watcherAndDisposables.RequestCancelledForAnyReasonCancellationToken.IsCancellationRequested)
                {
                    // TODO: test
                    if (!watcherAndDisposables.watcher.SenderCancelledTheRequest)
                    {
                        log.Write(EventType.Diagnostic, "Response for request {0}, has been overridden with an abandon message as the request was abandoned", requestActivityId);
                        response = ResponseMessage.FromException(response, new HalibutClientException(RequestAbandonedMessage));
                    }
                }
                var payload = await messageReaderWriter.PrepareResponse(response, cancellationToken);
                log.Write(EventType.MessageExchange, "Sending response message for request {0}", requestActivityId);
                await PollAndSubscribeToResponse.TrySendMessage(ResponseMessageSubscriptionName, halibutRedisTransport, endpoint, requestActivityId, payload, TTLOfResponseMessage, log);
                log.Write(EventType.MessageExchange, "Successfully applied response for request {0}", requestActivityId);
            }
            catch (Exception ex)
            {
                log.WriteException(EventType.Error, "Error applying response for request {0}", ex, requestActivityId);
                throw;
            }
            finally
            {
                    log.Write(EventType.Diagnostic, "Disposing in-flight request resources for request {0}", requestActivityId);
                    if (watcherAndDisposables != null)
                    {
                        await watcherAndDisposables.DisposeAsync();
                    }
            }
            
        }

        async Task<RequestMessage?> DequeueNextAsync()
        {
            
            await using var cts = new CancelOnDisposeCancellationToken(queueToken);
            try
            {
                // TODO can we avoid going to redis here?
                // TODO: Does this work well for multiple clients? We might go round before we collect work.
                // TODO: test this.
                hasItemsForEndpoint.Reset();
                
                var first = await TryRemoveNextItemFromQueue(cts.Token);
                if (first != null) return first;
                

                await Task.WhenAny(
                    hasItemsForEndpoint.WaitAsync(cts.Token), 
                    Task.Delay(halibutTimeoutsAndLimits.PollingQueueWaitTimeout, cts.Token));

                if (!hasItemsForEndpoint.IsSet)
                {
                    // Timed out waiting for something to go on the queue, send back a null to tentacle
                    // to keep the connection healthy.
                    return null;
                }
                
                return await TryRemoveNextItemFromQueue(cts.Token);
            }
            finally
            {
                await cts.CancelAsync();
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