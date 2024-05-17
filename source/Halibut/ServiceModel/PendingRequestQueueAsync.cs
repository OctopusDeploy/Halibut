using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.Exceptions;
using Halibut.Transport;
using Halibut.Transport.Protocol;
using Nito.AsyncEx;

namespace Halibut.ServiceModel
{
    public class PendingRequestQueueAsync : IPendingRequestQueue, IAsyncDisposable
    {
        readonly List<PendingRequest> queue = new();
        readonly Dictionary<string, PendingRequest> inProgress = new();
        readonly SemaphoreSlim queueLock = new(1, 1);
        readonly AsyncManualResetEvent itemAddedToQueue = new(false);
        readonly ILog log;
        readonly TimeSpan pollingQueueWaitTimeout;
        readonly CancellationTokenSource entireQueueCancellationTokenSource = new();

        public PendingRequestQueueAsync(HalibutTimeoutsAndLimits halibutTimeoutsAndLimits, ILog log) : this(
            log, 
            halibutTimeoutsAndLimits.PollingQueueWaitTimeout)
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
            using var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, this.entireQueueCancellationTokenSource.Token);
            cancellationToken = cancellationTokenSource.Token;
            
            using var pending = new PendingRequest(request, log);

            try
            {
                using (await queueLock.LockAsync(cancellationToken))
                {
                    queue.Add(pending);
                    inProgress.Add(request.Id, pending);
                    itemAddedToQueue.Set();
                }
            }
            catch (OperationCanceledException ex)
            {
                throw new ConnectingRequestCancelledException(ex);
            }

            try
            {
                await pending.WaitUntilComplete(cancellationToken);
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

        public async Task<RequestMessageWithCancellationToken?> DequeueAsync(CancellationToken cancellationToken)
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
                    return new (pending.Request, pending.PendingRequestCancellationToken);
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
            readonly ILog log;
            readonly AsyncManualResetEvent responseWaiter = new(false);
            readonly SemaphoreSlim transferLock = new(1, 1);
            bool transferBegun;
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

            public RequestMessage Request => request;

            public async Task WaitUntilComplete(CancellationToken cancellationToken)
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

                    using (await transferLock.LockAsync(CancellationToken.None))
                    {
                        if (!transferBegun)
                        {
                            completed = true;
                            log.Write(EventType.MessageExchange, "Request {0} was cancelled before it could be collected by the polling endpoint", request);
                            throw;
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
                        if (transferBegun && cancelTheRequestWhenTransferHasBegun)
                        {
                            // Cancel the dequeued request. This will cause co-operative cancellation on the thread dequeuing the request
                            pendingRequestCancellationTokenSource.Cancel();
                        }
                        else if (!transferBegun)
                        {
                            // Cancel the queued request. This will flag the request as cancelled to stop it being dequeued
                            pendingRequestCancellationTokenSource.Cancel();
                        }

                        if (timeoutCancellationTokenSource.IsCancellationRequested)
                        {
                            return false;
                        }
                    
                        throw transferBegun ? new TransferringRequestCancelledException(ex) : new ConnectingRequestCancelledException(ex);
                    }
                }

                return true;
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
                        // Check if the request has already been completed or if the request has been cancelled 
                        // to ensure we don't dequeue an already completed or already cancelled request
                        if (completed || pendingRequestCancellationTokenSource.IsCancellationRequested)
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
            public CancellationToken PendingRequestCancellationToken { get; }

            public void SetResponse(ResponseMessage response)
            {
                this.response = response;
                responseWaiter.Set();
            }

            public void Dispose()
            {
                pendingRequestCancellationTokenSource?.Dispose();
                transferLock?.Dispose();
            }
        }

        public ValueTask DisposeAsync()
        {
            entireQueueCancellationTokenSource.Cancel();
            entireQueueCancellationTokenSource.Dispose();
            return new ValueTask();
        }
    }
}