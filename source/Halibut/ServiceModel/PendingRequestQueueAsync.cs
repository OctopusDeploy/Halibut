using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.Transport.Protocol;
using Nito.AsyncEx;

namespace Halibut.ServiceModel
{
    public class PendingRequestQueueAsync : IPendingRequestQueue
    {
        readonly List<PendingRequest> queue = new();
        readonly Dictionary<string, PendingRequest> inProgress = new();
        readonly SemaphoreSlim queueLock = new(1, 1);
        readonly AsyncManualResetEvent itemAddedToQueue = new(false);
        readonly ILog log;
        readonly TimeSpan pollingQueueWaitTimeout;
        readonly bool relyOnConnectionTimeoutsInsteadOfPollingRequestMaximumMessageProcessingTimeout;

        public PendingRequestQueueAsync(HalibutTimeoutsAndLimits halibutTimeoutsAndLimits, ILog log) : this(
            log, 
            halibutTimeoutsAndLimits.PollingQueueWaitTimeout, 
            halibutTimeoutsAndLimits.RelyOnConnectionTimeoutsInsteadOfPollingRequestMaximumMessageProcessingTimeout)
        {
            this.log = log;
        }

        public PendingRequestQueueAsync(ILog log, TimeSpan pollingQueueWaitTimeout, bool relyOnConnectionTimeoutsInsteadOfPollingRequestMaximumMessageProcessingTimeout)
        {
            this.log = log;
            this.pollingQueueWaitTimeout = pollingQueueWaitTimeout;
            this.relyOnConnectionTimeoutsInsteadOfPollingRequestMaximumMessageProcessingTimeout = relyOnConnectionTimeoutsInsteadOfPollingRequestMaximumMessageProcessingTimeout;
        }

        public async Task<ResponseMessage> QueueAndWaitAsync(RequestMessage request, RequestCancellationTokens requestCancellationTokens)
        {
            using var pending = new PendingRequest(request, relyOnConnectionTimeoutsInsteadOfPollingRequestMaximumMessageProcessingTimeout, log);

            using (await queueLock.LockAsync(requestCancellationTokens.LinkedCancellationToken))
            {
                queue.Add(pending);
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
                    queue.Remove(pending);
                    inProgress.Remove(request.Id);
                }
            }

            return pending.Response;
        }

        public bool IsEmpty
        {
            get
            {
                using (queueLock.Lock(CancellationToken.None))
                {
                    return queue.Count == 0;
                }

            }
        }

        public int Count
        {
            get
            {
                using (queueLock.Lock(CancellationToken.None))
                {
                    return queue.Count;
                }
            }
        }

        public async Task<RequestMessage?> DequeueAsync(CancellationToken cancellationToken)
        {
            var timer = Stopwatch.StartNew();

            while (true)
            {
                var timeout = pollingQueueWaitTimeout - timer.Elapsed;
                var pending = await DequeueNextAsync(timeout, cancellationToken);
                if (pending == null)
                {
                    return null;
                }

                var result = await pending.BeginTransfer();
                if (result)
                {
                    return pending.Request;
                }
            }
        }

        async Task<PendingRequest?> DequeueNextAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            var first = await TakeFirst(cancellationToken);
            if (first != null || timeout <= TimeSpan.Zero)
            {
                return first;
            }

            using var cleanupCancellationTokenSource = new CancellationTokenSource();
            using var linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cleanupCancellationTokenSource.Token);

            await Task.WhenAny(
                // ReSharper disable once MethodSupportsCancellation
                itemAddedToQueue.WaitAsync( /*Do not pass a cancellation token as it will increase memory usage and may cause memory leaks*/),
                Task.Delay(timeout, linkedCancellationTokenSource.Token));

            cleanupCancellationTokenSource.Cancel();

            itemAddedToQueue.Reset();

            return await TakeFirst(cancellationToken);
        }

        async Task<PendingRequest?> TakeFirst(CancellationToken cancellationToken)
        {
            using (await queueLock.LockAsync(cancellationToken))
            {
                if (queue.Count == 0)
                {
                    return null;
                }

                var first = queue[0];
                queue.RemoveAt(0);

                if (queue.Count == 0)
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

        class PendingRequest : IDisposable
        {
            readonly RequestMessage request;
            readonly bool relyOnConnectionTimeoutsInsteadOfPollingRequestMaximumMessageProcessingTimeout;
            readonly ILog log;
            readonly AsyncManualResetEvent responseWaiter = new(false);
            readonly SemaphoreSlim transferLock = new(1, 1);
            bool transferBegun;
            bool completed;
            ResponseMessage? response;

            public PendingRequest(RequestMessage request, bool relyOnConnectionTimeoutsInsteadOfPollingRequestMaximumMessageProcessingTimeout, ILog log)
            {
                this.request = request;
                this.relyOnConnectionTimeoutsInsteadOfPollingRequestMaximumMessageProcessingTimeout = relyOnConnectionTimeoutsInsteadOfPollingRequestMaximumMessageProcessingTimeout;
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
                    responseSet = await WaitForResponseToBeSetForProcessingMessage(request.Destination.PollingRequestMaximumMessageProcessingTimeout, requestCancellationTokens.InProgressRequestCancellationToken);
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

            async Task<bool> WaitForResponseToBeSetForProcessingMessage(TimeSpan requestTimeout, CancellationToken cancellationToken)
            {
                if (relyOnConnectionTimeoutsInsteadOfPollingRequestMaximumMessageProcessingTimeout)
                {
                    await WaitForResponseToBeSet(cancellationToken);
                    return true;
                }
                return await WaitForResponseToBeSet(requestTimeout, cancellationToken);
            }

            async Task<bool> WaitForResponseToBeSet(TimeSpan timeout, CancellationToken cancellationToken)
            {
                using var cancellationTokenSource = new CancellationTokenSource(timeout);
                try
                {
                    using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource.Token, cancellationToken);
                    await WaitForResponseToBeSet(linkedTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    if (cancellationTokenSource.IsCancellationRequested) return false;
                    throw;
                }

                return true;
            }

            async Task WaitForResponseToBeSet(CancellationToken cancellationToken)
            {
                await responseWaiter.WaitAsync(cancellationToken);
            }

            public async Task<bool> BeginTransfer()
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
                        if (completed)
                        {
                            return false;
                        }

                        transferBegun = true;
                        return true;
                    }
                }
                catch (ObjectDisposedException)
                {
                    return false;
                }
            }

            public ResponseMessage Response => response ?? throw new InvalidOperationException("Response has not been set.");

            public void SetResponse(ResponseMessage response)
            {
                this.response = response;
                responseWaiter.Set();
            }

            public void Dispose()
            {
                transferLock?.Dispose();
            }
        }
    }
}