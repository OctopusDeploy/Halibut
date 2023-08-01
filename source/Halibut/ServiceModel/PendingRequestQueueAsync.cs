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
        readonly AsyncManualResetEvent hasItems = new(false);
        readonly ILog log;
        readonly TimeSpan pollingQueueWaitTimeout;

        public PendingRequestQueueAsync(ILog log) : this(log, HalibutLimits.PollingQueueWaitTimeout)
        {
            this.log = log;
        }

        public PendingRequestQueueAsync(ILog log, TimeSpan pollingQueueWaitTimeout)
        {
            this.log = log;
            this.pollingQueueWaitTimeout = pollingQueueWaitTimeout;
        }
        
        public async Task<ResponseMessage> QueueAndWaitAsync(RequestMessage request, CancellationToken cancellationToken)
        {
            var pending = new PendingRequest(request, log);
            
            await queueLock.WaitAsync(cancellationToken);
            try
            {
                queue.Enqueue(pending);
                inProgress.Add(request.Id, pending);
                hasItems.Set();
            }
            finally
            {
                queueLock.Release();
            }
            
            try
            {
                await pending.WaitUntilComplete(cancellationToken);
            }
            finally
            {
                await queueLock.WaitAsync(CancellationToken.None);
                try
                {
                    inProgress.Remove(request.Id);
                    //queue.//REMOVE?
                }
                finally
                {
                    queueLock.Release();
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

            await Task.WhenAny(hasItems.WaitAsync(cancellationToken), Task.Delay(pollingQueueWaitTimeout, cancellationToken));
            hasItems.Reset();
            return await TakeFirst(cancellationToken);
        }

        async Task<PendingRequest> TakeFirst(CancellationToken cancellationToken)
        {
            await queueLock.WaitAsync(cancellationToken);
            try
            {
                if (!queue.TryDequeue(out var first))
                {
                    return null;
                }

                //TODO VERIFY
                if (queue.IsEmpty)
                {
                    hasItems.Reset();
                }

                return first;
            }
            finally
            {
                queueLock.Release();
            }
        }

        public async Task ApplyResponse(ResponseMessage response, ServiceEndPoint destination)
        {
            if (response == null)
            {
                return;
            }

            await queueLock.WaitAsync(CancellationToken.None);
            try
            {
                if (inProgress.TryGetValue(response.Id, out var pending))
                {
                    pending.SetResponse(response);
                }
            }
            finally
            {
                queueLock.Release();
            }
        }

        class PendingRequest
        {
            readonly RequestMessage request;
            readonly ILog log;
            
            readonly AsyncManualResetEvent waiter = new (false);
            readonly SemaphoreSlim requestLock = new(1, 1);
            bool transferBegun;
            bool completed;

            public PendingRequest(RequestMessage request, ILog log)
            {
                this.request = request;
                this.log = log;
            }

            public RequestMessage Request => request;

            public async Task WaitUntilComplete(CancellationToken cancellationToken)
            {
                log.Write(EventType.MessageExchange, "Request {0} was queued", request);

                bool success;
                var cancelled = false;

                try
                {
                    success = await WaitAsync(request.Destination.PollingRequestQueueTimeout, cancellationToken);
                    if (success)
                    {
                        log.Write(EventType.MessageExchange, "Request {0} was collected by the polling endpoint", request);
                        return;
                    }
                }
                catch (Exception ex) when (ex is TaskCanceledException or OperationCanceledException)
                {
                    // waiter.Set is only called when the request has been collected and the response received.
                    // It is possible that the transfer has already started once the cancellationToken is cancelled
                    // In this case we cannot walk away from the request as it is already in progress and no longer in the connecting phase
                    cancelled = true;
                    
                    await requestLock.WaitAsync(CancellationToken.None);
                    try
                    {
                        if (!transferBegun)
                        {
                            completed = true;
                            log.Write(EventType.MessageExchange, "Request {0} was cancelled before it could be collected by the polling endpoint", request);
                            throw;
                        }

                        log.Write(EventType.MessageExchange, "Request {0} was cancelled after it had been collected by the polling endpoint and will not be cancelled", request);
                    }
                    finally
                    {
                        requestLock.Release();
                    }
                }

                var waitForTransferToComplete = false;
                await requestLock.WaitAsync(CancellationToken.None);
                try
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
                finally
                {
                    requestLock.Release();
                }

                if (waitForTransferToComplete)
                {
                    success = await WaitAsync(request.Destination.PollingRequestMaximumMessageProcessingTimeout, CancellationToken.None);
                    if (success)
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
                        SetResponse(ResponseMessage.FromException(request, new TimeoutException($"A request was sent to a polling endpoint, the polling endpoint collected it but did not respond in the allowed time ({request.Destination.PollingRequestMaximumMessageProcessingTimeout}), so the request timed out.")));
                    }
                }
                else
                {
                    log.Write(EventType.MessageExchange, "Request {0} timed out before it could be collected by the polling endpoint", request);
                    SetResponse(ResponseMessage.FromException(request, new TimeoutException($"A request was sent to a polling endpoint, but the polling endpoint did not collect the request within the allowed time ({request.Destination.PollingRequestQueueTimeout}), so the request timed out.")));
                }
            }

            async Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
            {
                using var cancellationTokenSource = new CancellationTokenSource(timeout);
                try
                {
                    var token = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource.Token, cancellationToken).Token;
                    await waiter.WaitAsync(token);
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
                await requestLock.WaitAsync(CancellationToken.None);
                try
                {
                    if (completed)
                    {
                        return false;
                    }

                    transferBegun = true;
                    return true;
                }
                finally
                {
                    requestLock.Release();
                }
            }

            public ResponseMessage Response { get; private set; }

            public void SetResponse(ResponseMessage response)
            {
                Response = response;
                waiter.Set();
            }
        }
    }
}