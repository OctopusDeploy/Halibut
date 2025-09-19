
#if NET8_0_OR_GREATER
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.Queue.QueuedDataStreams;
using Halibut.Queue.Redis.Cancellation;
using Halibut.Queue.Redis.Exceptions;
using Halibut.Queue.Redis.MessageStorage;
using Halibut.Queue.Redis.NodeHeartBeat;
using Halibut.Queue.Redis.RedisDataLossDetection;
using Halibut.Queue.Redis.RedisHelpers;
using Halibut.Queue.Redis.ResponseMessageTransfer;
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
        readonly IHalibutRedisTransport halibutRedisTransport;
        readonly HalibutTimeoutsAndLimits halibutTimeoutsAndLimits;
        readonly IMessageSerialiserAndDataStreamStorage messageSerialiserAndDataStreamStorage;
        readonly AsyncManualResetEvent hasItemsForEndpoint = new();

        readonly CancelOnDisposeCancellationToken queueCts = new();
        internal ConcurrentDictionary<Guid, WatcherAndDisposables> DisposablesForInFlightRequests = new();

        readonly CancellationToken queueToken;

        // Used for testing.
        int numberOfInFlightRequestsThatHaveReachedTheStageOfBeingReadyForCollection = 0;

        Task<IAsyncDisposable> RequestMessageAvailablePulseChannelSubscriberDisposer { get; }

        public bool IsEmpty => Count == 0;
        public int Count => numberOfInFlightRequestsThatHaveReachedTheStageOfBeingReadyForCollection;

        // The timespan is more generous for the sender going offline, since if it does go offline,
        // under some cases the request completing is advantageous. That node needs to
        // re-do the entire RPC for idempotent RPCs this might mean that the task required is already done.
        internal TimeSpan RequestSenderNodeHeartBeatTimeout { get; set; } = TimeSpan.FromSeconds(90);

        // How often the Request Sender sends a heart beat.
        internal TimeSpan RequestSenderNodeHeartBeatRate { get; set; } = TimeSpan.FromSeconds(15);

        /// <summary>
        /// The amount of time since the last heart beat from the node sending the request to Tentacle
        /// before the node is assumed to be offline.
        ///
        /// Setting this too high means things above the RPC might not have time to retry.
        /// </summary>
        public TimeSpan RequestReceiverNodeHeartBeatTimeout { get; set; } = TimeSpan.FromSeconds(60);

        // How often the Request Receiver node sends a heart beat.
        internal TimeSpan RequestReceiverNodeHeartBeatRate { get; set; } = TimeSpan.FromSeconds(15);

        // How long the response message can live in redis.
        internal TimeSpan TTLOfResponseMessage { get; set; } = TimeSpan.FromMinutes(20);

        internal TimeSpan TimeBetweenCheckingIfRequestWasCollected { get; set; } = TimeSpan.FromSeconds(15);

        /// <summary>
        /// How long to delay before we will start sending or checking for heart beat pulses.
        /// Note that the Node Sending the request won't send heart beats until it has detected
        /// the request has been collected. Which won't be detected until TimeBetweenCheckingIfRequestWasCollected
        /// has passed.
        ///
        /// 7s is chosen since Tentacle Client has a 5s wait for script delay, and 7s just exceeds that. This
        /// should be enough time that most of the time we never need to send or check for heart beat messages
        /// reducing load on the queue.
        /// </summary>
        static readonly HeartBeatInitialDelay DefaultHeartBeatInitialDelay = new(TimeSpan.FromSeconds(7));

        public HeartBeatInitialDelay HeartBeatInitialDelay { get; set; } = DefaultHeartBeatInitialDelay;

        /// <summary>
        /// With this delay short requests (sub 5s) may not be cancelled if the cancellation occurs after the request
        /// has been collected by the other side.
        /// Note that Halibut's cancellation does NOT mean that the other side will stop processing the request once
        /// cancellation is sent. instead all it can do is terminate the network stream. Which really means,
        /// we can only stop transferring of a request or a response.
        /// Thus setting this to a value greater than zero will have very little impact since, it is very unlikely
        /// we could have stopped the request from being executed by the service anyway.
        /// 7 seconds is chosen since, for our use cases most requests are done within that time range.
        /// </summary>
        static readonly DelayBeforeSubscribingToRequestCancellation DefaultDelayBeforeSubscribingToRequestCancellation = new(TimeSpan.FromSeconds(7));

        public DelayBeforeSubscribingToRequestCancellation DelayBeforeSubscribingToRequestCancellation { get; set; } = DefaultDelayBeforeSubscribingToRequestCancellation;

        public RedisPendingRequestQueue(
            Uri endpoint,
            IWatchForRedisLosingAllItsData watchForRedisLosingAllItsData,
            ILog log,
            IHalibutRedisTransport halibutRedisTransport,
            IMessageSerialiserAndDataStreamStorage messageSerialiserAndDataStreamStorage,
            HalibutTimeoutsAndLimits halibutTimeoutsAndLimits)
        {
            this.endpoint = endpoint;
            this.watchForRedisLosingAllItsData = watchForRedisLosingAllItsData;
            this.log = log.ForContext<RedisPendingRequestQueue>();
            this.messageSerialiserAndDataStreamStorage = messageSerialiserAndDataStreamStorage;
            this.halibutRedisTransport = halibutRedisTransport;
            this.halibutTimeoutsAndLimits = halibutTimeoutsAndLimits;
            this.queueToken = queueCts.Token;

            // Ideally we would only subscribe subscribers which are using this queue.
            RequestMessageAvailablePulseChannelSubscriberDisposer = Task.Run(async () => await this.halibutRedisTransport.SubscribeToRequestMessagePulseChannel(endpoint, _ => hasItemsForEndpoint.Set(), queueToken));
        }

        internal async Task WaitUntilQueueIsSubscribedToReceiveMessages() => await RequestMessageAvailablePulseChannelSubscriberDisposer;

        async Task<CancellationToken> DataLossCancellationToken(CancellationToken? cancellationToken)
        {
            // Try to get the token immediately if monitoring is already active
            var token = watchForRedisLosingAllItsData.TryGetTokenForDataLossDetection();
            if (token.HasValue)
            {
                return token.Value;
            }

            // Fall back to waiting for the token if not immediately available
            await using var cts = new CancelOnDisposeCancellationToken(queueCts.Token, cancellationToken ?? CancellationToken.None);
            return await watchForRedisLosingAllItsData.GetTokenForDataLossDetection(TimeSpan.FromSeconds(30), cts.Token);
        }

        public async Task<ResponseMessage> QueueAndWaitAsync(RequestMessage request, CancellationToken requestCancellationToken)
        {
            CancellationToken dataLossCt;
            try
            {
                dataLossCt = await DataLossCancellationToken(requestCancellationToken);
            }
            catch (Exception ex)
            {
                if (requestCancellationToken.IsCancellationRequested) throw RedisPendingRequest.CreateExceptionForRequestWasCancelledBeforeCollected(request, log);
                throw new CouldNotGetDataLossTokenInTimeHalibutClientException("Unable to reconnect to redis to get data loss detection CT", ex);
            }

            Exception? CancellationReason()
            {
                if (dataLossCt.IsCancellationRequested) return new RedisDataLossHalibutClientException($"Request {request.ActivityId} was cancelled because we detected that redis lost all of its data.");
                if (queueToken.IsCancellationRequested) return new RedisQueueShutdownClientException($"Request {request.ActivityId} was cancelled because the queue is shutting down.");
                return null;
            }

            Exception? CreateCancellationExceptionIfCancelled()
            {
                if (requestCancellationToken.IsCancellationRequested) return RedisPendingRequest.CreateExceptionForRequestWasCancelledBeforeCollected(request, log);
                return CancellationReason();
            }


            await using var cts = new CancelOnDisposeCancellationToken(queueCts.Token, requestCancellationToken, dataLossCt);
            var cancellationToken = cts.Token;

            using var pending = new RedisPendingRequest(request, log);

            RedisStoredMessage messageToStore;
            HeartBeatDrivenDataStreamProgressReporter heartBeatDrivenDataStreamProgressReporter;
            try
            {
                (messageToStore, heartBeatDrivenDataStreamProgressReporter) = await messageSerialiserAndDataStreamStorage.PrepareRequest(request, cancellationToken);
            }
            catch (Exception ex)
            {
                throw CreateCancellationExceptionIfCancelled()
                      ?? new ErrorWhilePreparingRequestForQueueHalibutClientException($"Request {request.ActivityId} failed since an error occured when preparing request for queue", ex);
            }

            await using var _ = heartBeatDrivenDataStreamProgressReporter; // Disposal of the reporter notifies all DataStream progress reportors that the upload is complete.


            // Start listening for a response to the request, we don't want to miss the response.
            await using var pollAndSubscribeToResponse = new PollAndSubscribeToResponse(endpoint, request.ActivityId, halibutRedisTransport, log);

            var tryClearRequestFromQueueAtMostOnce = new AsyncLazy<bool>(async () => await TryClearRequestFromQueue(pending));
            try
            {
                await using var senderPulse = new NodeHeartBeatSender(endpoint, request.ActivityId, halibutRedisTransport, log, HalibutQueueNodeSendingPulses.RequestSenderNode, () => new HeartBeatMessage(), RequestSenderNodeHeartBeatRate, HeartBeatInitialDelay);
                // Make the request available before we tell people it is available.
                try
                {
                    await halibutRedisTransport.PutRequest(endpoint, request.ActivityId, messageToStore, request.Destination.PollingRequestQueueTimeout, cancellationToken);
                    await halibutRedisTransport.PushRequestGuidOnToQueue(endpoint, request.ActivityId, cancellationToken);
                    await halibutRedisTransport.PulseRequestPushedToEndpoint(endpoint, cancellationToken);
                }
                catch (Exception ex)
                {
                    throw CreateCancellationExceptionIfCancelled()
                          ?? new ErrorOccuredWhenInsertingDataIntoRedisHalibutPendingRequestQueueHalibutClientException($"Request {request.ActivityId} failed since an error occured inserting the data into the queue", ex);
                }

                Interlocked.Increment(ref numberOfInFlightRequestsThatHaveReachedTheStageOfBeingReadyForCollection);
                try
                {
                    // We must be careful here to ensure we will always return.

                    var watchProcessingNodeStillHasHeartBeat = WatchProcessingNodeIsStillConnectedInBackground(request, pending, heartBeatDrivenDataStreamProgressReporter, cancellationToken);
                    var waitingForResponse = WaitForResponse(pollAndSubscribeToResponse, request, cancellationToken);
                    var pendingRequestWaitUntilComplete = pending.WaitUntilComplete(
                        async () => await tryClearRequestFromQueueAtMostOnce.Task,
                        CancellationReason,
                        cancellationToken);

                    cts.AwaitTasksBeforeCTSDispose(watchProcessingNodeStillHasHeartBeat, waitingForResponse, pendingRequestWaitUntilComplete);

                    await Task.WhenAny(waitingForResponse, pendingRequestWaitUntilComplete, watchProcessingNodeStillHasHeartBeat);

                    if (pendingRequestWaitUntilComplete.IsCompleted || cancellationToken.IsCancellationRequested)
                    {
                        await pendingRequestWaitUntilComplete;
                        return pending.Response!;
                    }

                    if (waitingForResponse.IsCompleted)
                    {
                        var response = await waitingForResponse;
                        if (response != null)
                        {
                            return await pending.SetResponse(response);
                        }
                        else if (!cancellationToken.IsCancellationRequested)
                        {
                            // We are no longer waiting for a response and have no response.
                            // The cancellation token has not been set so the request is not going to be cancelled.
                            // It is unclear how we got into this state, but lets at least error out.
                            return await pending.SetResponse(ResponseMessage.FromError(request, "Queue unexpectedly stopped waiting for a response"));
                        }
                    }

                    if (watchProcessingNodeStillHasHeartBeat.IsCompleted)
                    {
                        var watcherResult = await watchProcessingNodeStillHasHeartBeat;
                        if (watcherResult == NodeWatcherResult.NodeMayHaveDisconnected)
                        {
                            // Make a list ditch effort to check if a response exists now.
                            if (await pollAndSubscribeToResponse.TryGetResponseFromRedis("Watcher", cancellationToken))
                            {
                                var response = await waitingForResponse;
                                if (response != null)
                                {
                                    return await pending.SetResponse(response);
                                }
                            }

                            return await pending.SetResponse(ResponseMessage.FromError(request, "The node processing the request did not send a heartbeat for long enough, and so the node is now assumed to be offline."));
                        }
                    }

                    return await pending.SetResponse(ResponseMessage.FromError(request, "Impossible queue state reached"));
                }
                finally
                {
                    Interlocked.Decrement(ref numberOfInFlightRequestsThatHaveReachedTheStageOfBeingReadyForCollection);
                }
            }
            finally
            {
                InBackgroundSendCancellationIfRequestWasCancelled(request, pending);
                // Make an attempt to ensure the request is removed from redis, if we are unsure it was removed.
                var background = Task.Run(async () => await Try.IgnoringError(async () =>
                {
                    if (pending.HasRequestBeenMarkedAsCollected
                        || !pollAndSubscribeToResponse.ResponseJson.IsCompletedSuccessfully)
                    {
                        await tryClearRequestFromQueueAtMostOnce.Task;
                    }
                }));
            }
        }


        void InBackgroundSendCancellationIfRequestWasCancelled(RequestMessage request, RedisPendingRequest redisPending)
        {
            if (redisPending.PendingRequestCancellationToken.IsCancellationRequested)
            {
                log.Write(EventType.Diagnostic, "Request {0} was cancelled, sending cancellation to endpoint {1}", request.ActivityId, endpoint);
                Task.Run(async () => await RequestCancelledSender.TrySendCancellation(halibutRedisTransport, endpoint, request, log));
            }
            else
            {
                log.Write(EventType.Diagnostic, "Request {0} was not cancelled, no cancellation needed for endpoint {1}", request.ActivityId, endpoint);
            }
        }

        async Task<NodeWatcherResult?> WatchProcessingNodeIsStillConnectedInBackground(RequestMessage request, RedisPendingRequest redisPendingRequest, IGetNotifiedOfHeartBeats notifiedOfHeartBeats, CancellationToken cancellationToken)
        {
            await Task.Yield();

            return await NodeHeartBeatWatcher.WatchThatNodeProcessingTheRequestIsStillAlive(
                endpoint,
                request,
                redisPendingRequest,
                halibutRedisTransport,
                TimeBetweenCheckingIfRequestWasCollected,
                log,
                RequestReceiverNodeHeartBeatTimeout,
                notifiedOfHeartBeats,
                cancellationToken);
        }

        async Task<bool> TryClearRequestFromQueue(RedisPendingRequest redisPending)
        {
            var request = redisPending.Request;
            log.Write(EventType.Diagnostic, "Attempting to clear request {0} from queue for endpoint {1}", request.ActivityId, endpoint);

            // The time the message is allowed to sit on the queue for has elapsed.
            // Let's try to pop if from the queue, either:
            // - We pop it, which means it was never collected so let pending deal with the timeout.
            // - We could not pop it, which means it was collected.
            try
            {
                if (redisPending.HasRequestBeenMarkedAsCollected)
                {
                    log.Write(EventType.Diagnostic, "Request {0} has already been marked as collected, skipping queue removal for endpoint {1}", request.ActivityId, endpoint);
                    return false;
                }

                await using var cts = new CancelOnDisposeCancellationToken();
                cts.CancelAfter(TimeSpan.FromMinutes(2)); // Best efforts.
                var requestMessage = await halibutRedisTransport.TryGetAndRemoveRequest(endpoint, request.ActivityId, cts.Token);
                if (requestMessage != null)
                {
                    log.Write(EventType.Diagnostic, "Successfully removed request {0} from queue - request was never collected by a processing node", request.ActivityId);
                    return true;
                }
                else
                {
                    await redisPending.RequestHasBeenCollectedAndWillBeTransferred();
                    log.Write(EventType.Diagnostic, "Request {0} was not found in queue - it was already collected by a processing node", request.ActivityId);
                }
            }
            catch (Exception ex)
            {
                log.WriteException(EventType.Error, "Failed to clear request {0} from queue for endpoint {1}", ex, request.ActivityId, endpoint);
            }

            return false;
        }

        async Task<ResponseMessage?> WaitForResponse(
            PollAndSubscribeToResponse pollAndSubscribeToResponse,
            RequestMessage requestMessage,
            CancellationToken cancellationToken)
        {
            await Task.Yield();
            var activityId = requestMessage.ActivityId;
            RedisStoredMessage responseJson;
            try
            {
                log.Write(EventType.Diagnostic, "Waiting for response for request {0}", activityId);
                responseJson = await pollAndSubscribeToResponse.ResponseJson.WaitAsync(cancellationToken);
                log.Write(EventType.Diagnostic, "Received response JSON for request {0}, deserializing", activityId);
            }
            catch (Exception ex)
            {
                log.WriteException(EventType.Error, "Error while processing response for request {0}", ex, activityId);
                return null;
            }

            try
            {
                var response = await messageSerialiserAndDataStreamStorage.ReadResponseFromRedisStoredMessage(responseJson, cancellationToken);
                log.Write(EventType.Diagnostic, "Successfully deserialized response for request {0}", activityId);
                return response;
            }
            catch (Exception ex)
            {
                log.Write(EventType.Error, "Error deserializing response for request {0}", activityId);
                return ResponseMessage.FromException(requestMessage, new Exception("Error occured when reading data from the queue", ex));
            }
        }

        public async Task<RequestMessageWithCancellationToken?> DequeueAsync(CancellationToken cancellationToken)
        {
            // Is it good or bad that redis exceptions will bubble out of here?
            // It will kill the TCP connection, which will force re-connect (in perhaps a backoff function)
            // This could result in connecting to a node that is actually connected to redis. It could also
            // cause a cascade of failure from high load.
            var pending = await DequeueNextAsync();
            if (pending == null) return null;

            var (activityId, dataToSend, dataStreamsTransferProgress) = pending.Value;


            var disposables = new DisposableCollection();
            try
            {
                // There is a chance the data loss occured after we got the data but before here.
                // In that case we will just time out because of the lack of heart beats.
                var dataLossCT = await DataLossCancellationToken(cancellationToken);

                disposables.AddAsyncDisposable(new NodeHeartBeatSender(
                    endpoint,
                    activityId,
                    halibutRedisTransport,
                    log,
                    HalibutQueueNodeSendingPulses.RequestProcessorNode,
                    () => HeartBeatMessage.Build(dataStreamsTransferProgress),
                    RequestReceiverNodeHeartBeatRate,
                    HeartBeatInitialDelay));
                var watcher = new WatchForRequestCancellationOrSenderDisconnect(endpoint, activityId, halibutRedisTransport, RequestSenderNodeHeartBeatTimeout, HeartBeatInitialDelay, DelayBeforeSubscribingToRequestCancellation, log);
                disposables.AddAsyncDisposable(watcher);

                var cts = new CancelOnDisposeCancellationToken(watcher.RequestProcessingCancellationToken, dataLossCT);
                disposables.AddAsyncDisposable(cts);

                var response = new RequestMessageWithCancellationToken(dataToSend, cts.Token, activityId);
                DisposablesForInFlightRequests[activityId] = new WatcherAndDisposables(disposables, cts.Token, watcher);
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
            public WatchForRequestCancellationOrSenderDisconnect Watcher { get; }

            public WatcherAndDisposables(DisposableCollection disposableCollection, CancellationToken requestCancelledForAnyReasonCancellationToken, WatchForRequestCancellationOrSenderDisconnect watcher)
            {
                this.disposableCollection = disposableCollection;
                this.RequestCancelledForAnyReasonCancellationToken = requestCancelledForAnyReasonCancellationToken;
                this.Watcher = watcher;
            }

            public async ValueTask DisposeAsync()
            {
                await Try.IgnoringError(async () => await disposableCollection.DisposeAsync());
            }
        }

        public const string RequestAbandonedMessage = "The request was abandoned, possibly because the node processing the request shutdown or redis lost all of its data.";

        public async Task ApplyResponse(ResponseMessage response, Guid requestActivityId)
        {
            await Task.CompletedTask;
            throw new NotImplementedException("This should not be called!");
        }

        public async Task ApplyRawResponse(ResponseBytesAndDataStreams response, Guid requestActivityId) {
            log.Write(EventType.MessageExchange, "Applying response for request {0}", requestActivityId);
            WatcherAndDisposables? watcherAndDisposables = null;
            if (!DisposablesForInFlightRequests.TryRemove(requestActivityId, out watcherAndDisposables))
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
                    if (!watcherAndDisposables.Watcher.SenderCancelledTheRequest)
                    {
                        log.Write(EventType.Diagnostic, "Response for request {0}, has been overridden with an abandon message as the request was abandoned", requestActivityId);
                        //response = ResponseMessage.FromUnknownRequest(requestActivityId, new HalibutClientException(RequestAbandonedMessage));
                        throw new Exception("TODO God help you!");
                    }
                }
                var responseStoredMessage = await messageSerialiserAndDataStreamStorage.PrepareResponseForStorageInRedis(requestActivityId, response, cancellationToken);
                log.Write(EventType.MessageExchange, "Sending response message for request {0}", requestActivityId);
                await ResponseMessageSender.SendResponse(halibutRedisTransport, endpoint, requestActivityId, responseStoredMessage, TTLOfResponseMessage, log);
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

        async Task<(Guid ActivityId, PreparedRequestMessage dataToSend, RequestDataStreamsTransferProgress)?> DequeueNextAsync()
        {
            await using var cts = new CancelOnDisposeCancellationToken(queueToken);
            try
            {
                hasItemsForEndpoint.Reset();

                var first = await TryRemoveNextItemFromQueue(cts.Token);
                if (first != null)
                {
                    return first;
                }

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
            catch (Exception ex)
            {
                if (!queueToken.IsCancellationRequested)
                {
                    log.WriteException(EventType.Error, "Error occured dequeuing from the queue", ex);
                    // It is very likely a queue error means every tentacle will return an error.
                    // Add a random delay to help avoid every client coming back at exactly the same time.
                    await DelayWithoutException.Delay(TimeSpan.FromSeconds(new Random().Next(15)), cts.Token);
                }
                throw;
            }
            finally
            {
                await cts.CancelAsync();
            }
        }

        async Task<(Guid, PreparedRequestMessage, RequestDataStreamsTransferProgress)?> TryRemoveNextItemFromQueue(CancellationToken cancellationToken)
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

                var (dataToSend, transferProgress) = await messageSerialiserAndDataStreamStorage.ReadRequest(jsonRequest, cancellationToken);
                log.Write(EventType.Diagnostic, "Successfully collected request {0} from queue for endpoint {1}", activityId, endpoint);

                return (activityId.Value, dataToSend, transferProgress);
            }
        }

        public async ValueTask DisposeAsync()
        {
            await Try.IgnoringError(async () => await queueCts.DisposeAsync());
            await Try.IgnoringError(async () => await (await RequestMessageAvailablePulseChannelSubscriberDisposer).DisposeAsync());
        }

        public void Dispose()
        {
            DisposeAsync().GetAwaiter().GetResult();
        }
    }
}
#endif