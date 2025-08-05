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
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.Exceptions;
using Halibut.Transport;
using Halibut.Transport.Protocol;
using Halibut.Util;
using Nito.AsyncEx;

namespace Halibut.ServiceModel
{
    public class PendingRequest : IDisposable
    {
        readonly RequestMessage request;
        readonly ILog log;
        readonly AsyncManualResetEvent responseWaiter = new(false);
        readonly SemaphoreSlim transferLock = new(1, 1);
        //bool transferBegun;
        AsyncManualResetEvent requestCollected = new(false);
        readonly CancellationTokenSource pendingRequestCancellationTokenSource;
        ResponseMessage? response;

        public PendingRequest(RequestMessage request, ILog log)
        {
            this.request = request;
            this.log = log;

            pendingRequestCancellationTokenSource = new CancellationTokenSource();
            PendingRequestCancellationToken = pendingRequestCancellationTokenSource.Token;
        }

        public Task WaitForRequestToBeMarkedAsCollected(CancellationToken cancellationToken) => requestCollected.WaitAsync(cancellationToken);
        
        public bool HasRequestBeenMarkedAsCollected => requestCollected.IsSet;
        
        public RequestMessage Request => request;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="timePendingRequestCanBeOnTheQueueHasElapsed">
        /// This will be called either when the pick-up timeout has elapsed OR if the Cancellation Token has been triggered.
        /// This gives the user an opportunity to remove the pending request from shared places and optionally
        /// call BeginTransfer
        /// </param>
        /// <param name="cancellationToken"></param>
        public async Task WaitUntilComplete(Func<Task> timePendingRequestCanBeOnTheQueueHasElapsed, CancellationToken cancellationToken)
        {
            log.Write(EventType.MessageExchange, "Request {0} was queued", request);

            await using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken).CancelOnDispose();
            
            var pendingRequestPickupTimeout = Try.IgnoringError(async () => await Task.Delay(request.Destination.PollingRequestQueueTimeout, cts.CancellationToken));
            var responseWaiterTask = responseWaiter.WaitAsync(cts.CancellationToken);
            
            await Task.WhenAny(pendingRequestPickupTimeout, responseWaiterTask);

            using (await transferLock.LockAsync(CancellationToken.None))
            {
                // Response has been returned so just say we are done.
                if (responseWaiter.IsSet)
                {
                    log.Write(EventType.MessageExchange, "Request {0} was collected by the polling endpoint", request);
                    return;
                }


                if (cancellationToken.IsCancellationRequested)
                {
                    if (!requestCollected.IsSet) log.Write(EventType.MessageExchange, "Request {0} was cancelled before it could be collected by the polling endpoint", request);
                    else log.Write(EventType.MessageExchange, "Request {0} was collected by the polling endpoint, will try to cancel the request", request);

                    await Try.IgnoringError(async () => await pendingRequestCancellationTokenSource.CancelAsync());
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }

            if (!requestCollected.IsSet) await timePendingRequestCanBeOnTheQueueHasElapsed();
            
            using (await transferLock.LockAsync(CancellationToken.None)) {
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
                        false);
                    return;
                }
            }
            
            
            // The request has been collected so now wait patiently for a response
            log.Write(EventType.MessageExchange, "Request {0} was eventually collected by the polling endpoint", request);
            try
            {
                await responseWaiterTask;
            }
            catch (Exception) when (!cts.CancellationToken.IsCancellationRequested)
            {
                using (await transferLock.LockAsync(CancellationToken.None))
                {
                    if (!responseWaiter.IsSet)
                    {
                        log.Write(EventType.MessageExchange, "Request {0} was cancelled before a response was received", request);
                        SetResponseNoLock(ResponseMessage.FromException(
                            request,
                            new TimeoutException($"A request was sent to a polling endpoint, the polling endpoint collected it but the request was cancelled before the polling endpoint responded."),
                            ConnectionState.Connecting),
                            false);
                        await Try.IgnoringError(async () => await pendingRequestCancellationTokenSource.CancelAsync());
                        cancellationToken.ThrowIfCancellationRequested();
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
                    false);
            }
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
                    if (requestCollected.IsSet 
                        || pendingRequestCancellationTokenSource.IsCancellationRequested
                        || responseWaiter.IsSet)
                        
                    {
                        return false;
                    }

                    requestCollected.Set();
                    return true;
                }
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
        }

        public ResponseMessage Response => response ?? throw new InvalidOperationException("Response has not been set.");
        public CancellationToken PendingRequestCancellationToken { get; }

        public void SetResponse(ResponseMessage response)
        {
            // If someone is calling this then we know for sure they collected the request
            this.SetResponseAsync(response, true).GetAwaiter().GetResult();
        }
        
        async Task SetResponseAsync(ResponseMessage response, bool requestWasCollected)
        {
            using (await transferLock.LockAsync(CancellationToken.None))
            {
                SetResponseNoLock(response, requestWasCollected);
            }
        }

        void SetResponseNoLock(ResponseMessage response, bool requestWasCollected)
        {
            if(this.response != null) return;
            this.response = response;
            responseWaiter.Set();
            if(requestWasCollected) requestCollected.Set(); // Also the request has been collected, if we have a response.
        }

        public void Dispose()
        {
            pendingRequestCancellationTokenSource?.Dispose();
            transferLock?.Dispose();
        }
    }
    
}