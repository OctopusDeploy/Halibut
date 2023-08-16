using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.Transport.Protocol;
using Nito.AsyncEx;

namespace Halibut.ServiceModel
{
    public class PendingRequestQueueAsync : IPendingRequestQueue
    {
        readonly ConcurrentQueue<PendingRequest> queue = new();
        readonly Dictionary<string, PendingRequest> inProgress = new();
        readonly SemaphoreSlim queueLock = new(1, 1);
        readonly AsyncManualResetEvent itemAddedToQueue = new(false);
        readonly ILog log;
        readonly TimeSpan pollingQueueWaitTimeout;

        public PendingRequestQueueAsync(HalibutTimeoutsAndLimits halibutTimeoutsAndLimits, ILog log) : this(log, halibutTimeoutsAndLimits.PollingQueueWaitTimeout)
        {
            this.log = log;
        }

        public PendingRequestQueueAsync(ILog log, TimeSpan pollingQueueWaitTimeout)
        {
            this.log = log;
            this.pollingQueueWaitTimeout = pollingQueueWaitTimeout;
        }

        [Obsolete]
        public async Task<ResponseMessage> QueueAndWaitAsync(RequestMessage request, CancellationToken queuedRequestCancellationToken)
        {
            await Task.CompletedTask;

            throw new NotSupportedException($"Use {nameof(QueueAndWaitAsync)} with {nameof(RequestCancellationTokens)}");
        }

        public async Task<ResponseMessage> QueueAndWaitAsync(RequestMessage request, RequestCancellationTokens requestCancellationTokens)
        {
            var pending = new PendingRequest(request, log);

            using (await queueLock.LockAsync(requestCancellationTokens.LinkedCancellationToken))
            {
                queue.Enqueue(pending);
                inProgress.Add(request.Id, pending);
                itemAddedToQueue.Set();
            }

            try
            {
                await pending.WaitUntilComplete(requestCancellationTokens);
            }
            finally
            {
                using (await queueLock.LockAsync(CancellationToken.None))
                {
                    inProgress.Remove(request.Id);
                }
            }

            return pending.Response;
        }

        public bool IsEmpty => queue.IsEmpty;
        public int Count => queue.Count;

        public async Task<RequestMessage> DequeueAsync(CancellationToken cancellationToken)
        {
            var pending = await DequeueNextAsync(cancellationToken);
            if (pending == null) return null;

            var result = await pending.BeginTransfer();
            return result ? pending.Request : null;
        }

        async Task<PendingRequest> DequeueNextAsync(CancellationToken cancellationToken)
        {
            var first = await TakeFirst(cancellationToken);
            if (first != null)
            {
                return first;
            }

            await Task.WhenAny(itemAddedToQueue.WaitAsync(cancellationToken), Task.Delay(pollingQueueWaitTimeout, cancellationToken));
            itemAddedToQueue.Reset();
            return await TakeFirst(cancellationToken);
        }

        async Task<PendingRequest> TakeFirst(CancellationToken cancellationToken)
        {
            using (await queueLock.LockAsync(cancellationToken))
            {
                if (!queue.TryDequeue(out var first))
                {
                    return null;
                }
                
                if (queue.IsEmpty)
                {
                    itemAddedToQueue.Reset();
                }

                return first;
            }
        }

        public async Task ApplyResponse(ResponseMessage response, ServiceEndPoint destination)
        {
            if (response == null)
            {
                return;
            }

            using (await queueLock.LockAsync(CancellationToken.None))
            {
                if (inProgress.TryGetValue(response.Id, out var pending))
                {
                    pending.SetResponse(response);
                }
            }
        }

        class PendingRequest
        {
            readonly RequestMessage request;
            readonly ILog log;
            readonly AsyncManualResetEvent responseWaiter = new (false);
            readonly SemaphoreSlim transferLock = new(1, 1);
            bool transferBegun;
            bool completed;

            public PendingRequest(RequestMessage request, ILog log)
            {
                this.request = request;
                this.log = log;
            }

            public RequestMessage Request => request;

            public async Task WaitUntilComplete(RequestCancellationTokens requestCancellationTokens)
            {
                log.Write(EventType.MessageExchange, "Request {0} was queued", request);

                bool responseSet;
                var cancelled = false;

                try
                {
                    responseSet = await WaitForResponseToBeSet(request.Destination.PollingRequestQueueTimeout, requestCancellationTokens.LinkedCancellationToken);
                    if (responseSet)
                    {
                        log.Write(EventType.MessageExchange, "Request {0} was collected by the polling endpoint", request);
                        return;
                    }
                }
                catch (Exception ex) when (ex is TaskCanceledException or OperationCanceledException)
                {
                    // responseWaiter.Set is only called when the request has been collected and the response received.
                    // It is possible that the transfer has already started once the requestCancellationTokens.LinkedCancellationToke is cancelled
                    // If the requestCancellationTokens.InProgressCancellationToken is Ct.None or not cancelled then
                    // we cannot walk away from the request as it is already in progress and no longer in the connecting phase
                    cancelled = true;
                    
                    using (await transferLock.LockAsync(CancellationToken.None))
                    {
                        if (!transferBegun)
                        {
                            completed = true;
                            log.Write(EventType.MessageExchange, "Request {0} was cancelled before it could be collected by the polling endpoint", request);
                            throw;
                        }

                        if (!requestCancellationTokens.CanCancelInProgressRequest())
                        {
                            log.Write(EventType.MessageExchange, "Request {0} was cancelled after it had been collected by the polling endpoint and will not be cancelled", request);
                        }
                    }
                }

                var waitForTransferToComplete = false;
                using (await transferLock.LockAsync(CancellationToken.None))
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
                    // We cannot use requestCancellationTokens.ConnectingCancellationToken here, because if we were cancelled, and the transfer has begun, we should attempt to wait for it.
                    responseSet = await WaitForResponseToBeSet(request.Destination.PollingRequestMaximumMessageProcessingTimeout, requestCancellationTokens.InProgressRequestCancellationToken);
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
                        if (requestCancellationTokens.InProgressRequestCancellationToken.IsCancellationRequested)
                        {
                            log.Write(EventType.MessageExchange, "Request {0} was cancelled before a response was received", request);
                            SetResponse(ResponseMessage.FromException(request, new TimeoutException($"A request was sent to a polling endpoint, the polling endpoint collected it but the request was cancelled before the polling endpoint responded.")));
                        }
                        else
                        {
                            log.Write(EventType.MessageExchange, "Request {0} timed out before it could be collected by the polling endpoint", request);
                            SetResponse(ResponseMessage.FromException(request, new TimeoutException($"A request was sent to a polling endpoint, the polling endpoint collected it but did not respond in the allowed time ({request.Destination.PollingRequestMaximumMessageProcessingTimeout}), so the request timed out.")));    
                        }
                    }
                }
                else
                {
                    log.Write(EventType.MessageExchange, "Request {0} timed out before it could be collected by the polling endpoint", request);
                    SetResponse(ResponseMessage.FromException(request, new TimeoutException($"A request was sent to a polling endpoint, but the polling endpoint did not collect the request within the allowed time ({request.Destination.PollingRequestQueueTimeout}), so the request timed out.")));
                }
            }

            async Task<bool> WaitForResponseToBeSet(TimeSpan timeout, CancellationToken cancellationToken)
            {
                using var cancellationTokenSource = new CancellationTokenSource(timeout);
                try
                {
                    var token = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource.Token, cancellationToken).Token;
                    await responseWaiter.WaitAsync(token);
                }
                catch (OperationCanceledException)
                {
                    if (cancellationTokenSource.IsCancellationRequested) return false;
                    throw;
                }
                
                return true;
            }

            public async Task<bool> BeginTransfer()
            {
                using (await transferLock.LockAsync(CancellationToken.None))
                {
                    if (completed)
                    {
                        return false;
                    }

                    transferBegun = true;
                    return true;
                }
            }

            public ResponseMessage Response { get; private set; }

            public void SetResponse(ResponseMessage response)
            {
                Response = response;
                responseWaiter.Set();
            }
        }
    }
}