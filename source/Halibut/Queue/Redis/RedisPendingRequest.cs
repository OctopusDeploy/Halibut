#if NET8_0_OR_GREATER
using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.Exceptions;
using Halibut.ServiceModel;
using Halibut.Transport;
using Halibut.Transport.Protocol;
using Halibut.Util;
using Nito.AsyncEx;

namespace Halibut.Queue.Redis
{
    public class RedisPendingRequest : IDisposable
    {
        readonly RequestMessage request;
        readonly ILog log;
        readonly AsyncManualResetEvent responseWaiter = new(false);
        readonly SemaphoreSlim transferLock = new(1, 1);
        readonly AsyncManualResetEvent requestCollected = new(false);
        readonly CancellationTokenSource pendingRequestCancellationTokenSource;
        ResponseMessage? response;

        public RedisPendingRequest(RequestMessage request, ILog log)
        {
            this.request = request;
            this.log = log.ForContext<RedisPendingRequest>();

            pendingRequestCancellationTokenSource = new CancellationTokenSource();
            PendingRequestCancellationToken = pendingRequestCancellationTokenSource.Token;
        }

        public Task WaitForRequestToBeMarkedAsCollected(CancellationToken cancellationToken) => requestCollected.WaitAsync(cancellationToken);
        
        public bool HasRequestBeenMarkedAsCollected => requestCollected.IsSet;
        
        public RequestMessage Request => request;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="checkIfPendingRequestWasCollectedOrRemoveIt">
        /// This will be called either when the pick-up timeout has elapsed OR if the Cancellation Token has been triggered.
        /// This gives the user an opportunity to remove the pending request from shared places and optionally
        /// call BeginTransfer
        /// </param>
        /// <param name="overrideCancellationReason">Should the cancellationToken be triggered, this allows for overriding
        /// the reason the cancellation token was triggered. The returned error will be thrown.</param>
        /// <param name="cancellationToken"></param>
        public async Task WaitUntilComplete(Func<Task> checkIfPendingRequestWasCollectedOrRemoveIt,
            Func<Exception?> overrideCancellationReason,
            CancellationToken cancellationToken)
        {
            log.Write(EventType.MessageExchange, "Request {0} was queued", request);

            var pendingRequestPickupTimeout = DelayWithoutException.Delay(request.Destination.PollingRequestQueueTimeout, cancellationToken);
            var responseWaiterTask = responseWaiter.WaitAsync(cancellationToken);
            
            await Task.WhenAny(pendingRequestPickupTimeout, responseWaiterTask);

            // Response has been returned so just say we are done.
            if (responseWaiter.IsSet)
            {
                log.Write(EventType.MessageExchange, "Request {0} was collected by the polling endpoint", request);
                return;
            }

            if (!requestCollected.IsSet)
            {
                await checkIfPendingRequestWasCollectedOrRemoveIt();
            }
            
            using (await transferLock.LockAsync(CancellationToken.None))
            {
                if (responseWaiter.IsSet)
                {
                    log.Write(EventType.MessageExchange, "Request {0} was collected by the polling endpoint", request);
                    return;
                }
                
                if (cancellationToken.IsCancellationRequested)
                {
                    await Try.IgnoringError(async () => await pendingRequestCancellationTokenSource.CancelAsync());
                    
                    var cancellationException = overrideCancellationReason();
                    if (cancellationException != null)
                    {
                        log.Write(EventType.MessageExchange, "Request {0} did not complete because: " + cancellationException.Message, request);
                        throw cancellationException;
                    }

                    OperationCanceledException operationCanceledException;
                    if (!requestCollected.IsSet)
                    {
                        operationCanceledException = CreateExceptionForRequestWasCancelledBeforeCollected(request, log);
                    }
                    else
                    {
                        log.Write(EventType.MessageExchange, "Request {0} was collected by the polling endpoint, will try to cancel the request", request);
                        operationCanceledException = new OperationCanceledException($"Request {request} was collected by the polling endpoint, will try to cancel the request");
                    }
                        
                    throw requestCollected.IsSet
                        ? new TransferringRequestCancelledException(operationCanceledException)
                        : new ConnectingRequestCancelledException(operationCanceledException);
                }
                
                if (!requestCollected.IsSet)
                {
                    // Request was not collected within the pickup time.
                    // Prevent anyone from processing the request further.
                    await Try.IgnoringError(async () => await pendingRequestCancellationTokenSource.CancelAsync());
                    
                    log.Write(EventType.MessageExchange, "Request {0} timed out before it could be collected by the polling endpoint", request);
                    SetResponseNoLock(ResponseMessage.FromException(
                            request,
                            new TimeoutException($"A request was sent to a polling endpoint, but the polling endpoint did not collect the request within the allowed time ({request.Destination.PollingRequestQueueTimeout}), so the request timed out."),
                            ConnectionState.Connecting),
                        requestWasCollected: false);
                    return;
                }
            }
            
            // The request has been collected so now wait patiently for a response
            log.Write(EventType.MessageExchange, "Request {0} was eventually collected by the polling endpoint", request);
            try
            {
                await responseWaiterTask;
            }
            catch (Exception) when (cancellationToken.IsCancellationRequested)
            {
                using (await transferLock.LockAsync(CancellationToken.None))
                {
                    if (!responseWaiter.IsSet)
                    {
                        var cancellationException = overrideCancellationReason();
                        if (cancellationException != null)
                        {
                            await Try.IgnoringError(async () => await pendingRequestCancellationTokenSource.CancelAsync());
                            log.Write(EventType.MessageExchange, "Request {0} did not complete because: " + cancellationException.Message, request);
                            throw cancellationException;
                        }
                        
                        log.Write(EventType.MessageExchange, "Request {0} was cancelled before a response was received", request);
                        SetResponseNoLock(ResponseMessage.FromException(
                                request,
                                new TimeoutException("A request was sent to a polling endpoint, the polling endpoint collected it but the request was cancelled before the polling endpoint responded.")),
                            requestWasCollected: false);
                        await Try.IgnoringError(async () => await pendingRequestCancellationTokenSource.CancelAsync());
                    }
                }
            }
            catch (Exception)
            {
                // This should never happen.
                log.Write(EventType.MessageExchange, "Request {0} had an internal error, unexpectedly stopped waiting for the response.", request);
                await SetResponseAsync(ResponseMessage.FromException(
                        request,
                        new PendingRequestQueueInternalException($"Request {request.Id} had an internal error, unexpectedly stopped waiting for the response.")),
                    requestWasCollected: false);
            }
        }

        public static OperationCanceledException CreateExceptionForRequestWasCancelledBeforeCollected(RequestMessage request, ILog log)
        {
            log.Write(EventType.MessageExchange, "Request {0} was cancelled before it could be collected by the polling endpoint", request);
            return new OperationCanceledException($"Request {request} was cancelled before it could be collected by the polling endpoint");
        }

        public async Task<bool> RequestHasBeenCollectedAndWillBeTransferred()
        {
            // The PendingRequest is Disposed at the end of QueueAndWaitAsync but a race condition 
            // exists in the current approach that means DequeueAsync could pick this request up after
            // it has been disposed. At that point we are no longer interested in the PendingRequest so 
            // this is "ok" and wrapping BeginTransfer in a try..catch.. ensures we don't error if the
            // race condition occurs and also stops the polling tentacle dequeuing the request successfully.
            try
            {
                using (await transferLock.LockAsync(CancellationToken.None))
                {
                    // Check if the request has already been completed or if the request has been cancelled 
                    // to ensure we don't dequeue an already completed or already cancelled request

                    var requestHasBeenCollected = this.requestCollected.IsSet;
                    requestCollected.Set();
                    return !requestHasBeenCollected
                           && !responseWaiter.IsSet
                           && !pendingRequestCancellationTokenSource.IsCancellationRequested;
                }
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
        }

        public ResponseMessage Response => response ?? throw new InvalidOperationException("Response has not been set.");
        public CancellationToken PendingRequestCancellationToken { get; }

        public async Task<ResponseMessage> SetResponse(ResponseMessage response)
        {
            // If someone is calling this then we know for sure they collected the request
            return await SetResponseAsync(response, requestWasCollected: true);
        }
        
        async Task<ResponseMessage> SetResponseAsync(ResponseMessage response, bool requestWasCollected)
        {
            using (await transferLock.LockAsync(CancellationToken.None))
            {
                return SetResponseNoLock(response, requestWasCollected);
            }
        }

        ResponseMessage SetResponseNoLock(ResponseMessage response, bool requestWasCollected)
        {
            if (this.response != null)
            {
                return this.response;
            }

            this.response = response;
            responseWaiter.Set();
            if (requestWasCollected)
            {
                requestCollected.Set(); // Also the request has been collected, if we have a response.
            }

            return this.response;
        }

        public void Dispose()
        {
            transferLock?.Dispose();
        }

        public bool HasResponseBeenSet() => responseWaiter.IsSet;
    }
    
}
#endif