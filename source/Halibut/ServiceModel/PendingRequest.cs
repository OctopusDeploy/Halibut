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
        bool completed;
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

            bool responseSet;
            var cancelled = false;

            try
            {
                responseSet = await WaitForResponseToBeSet(
                    request.Destination.PollingRequestQueueTimeout, 
                    // Don't cancel a dequeued request as we need to wait PollingRequestMaximumMessageProcessingTimeout for it to complete
                    cancelTheRequestWhenTransferHasBegun: false, 
                    cancellationToken);

                if (responseSet)
                {
                    log.Write(EventType.MessageExchange, "Request {0} was collected by the polling endpoint", request);
                    return;
                }
            }
            catch (RequestCancelledException)
            {
                cancelled = true;
                if(!requestCollected.IsSet) await timePendingRequestCanBeOnTheQueueHasElapsed();
                using (await transferLock.LockAsync(CancellationToken.None))
                {
                    if (!requestCollected.IsSet)
                    {
                        completed = true;
                        log.Write(EventType.MessageExchange, "Request {0} was cancelled before it could be collected by the polling endpoint", request);
                        throw;
                    }
                }
            }

            if(!requestCollected.IsSet) await timePendingRequestCanBeOnTheQueueHasElapsed();
            var waitForTransferToComplete = false;
            using (await transferLock.LockAsync(CancellationToken.None))
            {
                if (requestCollected.IsSet)
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
                responseSet = await WaitForResponseToBeSet(
                    null,
                    // Cancel the dequeued request to force Reads and Writes to be cancelled
                    cancelTheRequestWhenTransferHasBegun: true,
                    cancellationToken);

                if (responseSet)
                {
                    // We end up here when the request is cancelled but already being transferred so we need to adjust the log message accordingly
                    if (cancelled)
                    {
                        log.Write(EventType.MessageExchange, "Request {0} was collected by the polling endpoint", request);
                    }
                    else
                    {
                        log.Write(EventType.MessageExchange, "Request {0} was eventually collected by the polling endpoint", request);
                    }
                }
                else
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        log.Write(EventType.MessageExchange, "Request {0} was cancelled before a response was received", request);
                        SetResponse(ResponseMessage.FromException(
                            request, 
                            new TimeoutException($"A request was sent to a polling endpoint, the polling endpoint collected it but the request was cancelled before the polling endpoint responded."),
                            ConnectionState.Connecting));
                    }
                    else
                    {
                        // This should never happen.
                        log.Write(EventType.MessageExchange, "Request {0} had an internal error, unexpectedly stopped waiting for the response.", request);
                        SetResponse(ResponseMessage.FromException(
                            request, 
                            new PendingRequestQueueInternalException($"Request {request.Id} had an internal error, unexpectedly stopped waiting for the response.")));
                    }
                }
            }
            else
            {
                log.Write(EventType.MessageExchange, "Request {0} timed out before it could be collected by the polling endpoint", request);
                SetResponse(ResponseMessage.FromException(
                    request, 
                    new TimeoutException($"A request was sent to a polling endpoint, but the polling endpoint did not collect the request within the allowed time ({request.Destination.PollingRequestQueueTimeout}), so the request timed out."),
                    ConnectionState.Connecting));
            }
        }
        
        async Task<bool> WaitForResponseToBeSet(
            TimeSpan? timeout, 
            bool cancelTheRequestWhenTransferHasBegun, 
            CancellationToken cancellationToken)
        {
            using var timeoutCancellationTokenSource = timeout.HasValue ? new CancellationTokenSource(timeout.Value) : new CancellationTokenSource();
            using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(timeoutCancellationTokenSource.Token, cancellationToken);

            try
            {
                await responseWaiter.WaitAsync(linkedTokenSource.Token);
            }
            catch (OperationCanceledException ex)
            {
                using (await transferLock.LockAsync(CancellationToken.None))
                {
                    if (requestCollected.IsSet && cancelTheRequestWhenTransferHasBegun)
                    {
                        // Cancel the dequeued request. This will cause co-operative cancellation on the thread dequeuing the request
                        pendingRequestCancellationTokenSource.Cancel();
                    }
                    else if (!requestCollected.IsSet)
                    {
                        // Cancel the queued request. This will flag the request as cancelled to stop it being dequeued
                        pendingRequestCancellationTokenSource.Cancel();
                    }

                    if (timeoutCancellationTokenSource.IsCancellationRequested)
                    {
                        return false;
                    }
                
                    throw requestCollected.IsSet ? new TransferringRequestCancelledException(ex) : new ConnectingRequestCancelledException(ex);
                }
            }

            return true;
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
                    if (completed || pendingRequestCancellationTokenSource.IsCancellationRequested)
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
            lock (responseWaiter)
            {
                if(this.response != null) return;
                this.response = response;
                responseWaiter.Set();
            }
        }

        public void Dispose()
        {
            pendingRequestCancellationTokenSource?.Dispose();
            transferLock?.Dispose();
        }
    }
    
}