using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.Transport.Protocol;
using Halibut.Util.AsyncEx;

namespace Halibut.ServiceModel
{
    public class PendingRequestQueue : IPendingRequestQueue
    {
        readonly List<PendingRequest> queue = new();
        readonly Dictionary<string, PendingRequest> inProgress = new();
        readonly object sync = new();
        readonly AsyncManualResetEvent hasItems = new();
        readonly ILog log;

        public PendingRequestQueue(ILog log)
        {
            this.log = log;
        }

        [Obsolete]
        public ResponseMessage QueueAndWait(RequestMessage request)
        {
            return QueueAndWait(request, CancellationToken.None);
        }

        [Obsolete]
        public ResponseMessage QueueAndWait(RequestMessage request, CancellationToken cancellationToken)
        {
            var pending = new PendingRequest(request, log);

            lock (sync)
            {
                queue.Add(pending);
                inProgress.Add(request.Id, pending);
                hasItems.Set();
            }

            pending.WaitUntilComplete(cancellationToken);

            lock (sync)
            {
                inProgress.Remove(request.Id);
            }

            return pending.Response;
        }

        public async Task<ResponseMessage> QueueAndWaitAsync(RequestMessage request, CancellationToken cancellationToken)
        {
#pragma warning disable 612
            var responseMessage = QueueAndWait(request, cancellationToken);
#pragma warning restore 612
            return await Task.FromResult(responseMessage);
        }

        public bool IsEmpty
        {
            get
            {
                lock (sync)
                {
                    return queue.Count == 0;
                }
            }
        }


        public RequestMessage Dequeue()
        {
            var pending = DequeueNext();
            if (pending == null)
            {
                return null;
            }

            return pending.BeginTransfer() ? pending.Request : null;
        }

        PendingRequest DequeueNext()
        {
            var first = TakeFirst();
            if (first != null)
            {
                return first;
            }

            using (var cts = new CancellationTokenSource(HalibutLimits.PollingQueueWaitTimeout))
                hasItems.Wait(cts.Token);

            hasItems.Reset();
            return TakeFirst();
        }

        public async Task<RequestMessage> DequeueAsync()
        {
            var pending = await DequeueNextAsync();
            if (pending == null) return null;
            return pending.BeginTransfer() ? pending.Request : null;
        }

        async Task<PendingRequest> DequeueNextAsync()
        {
            var first = TakeFirst();
            if (first != null)
            {
                return first;
            }

            await Task.WhenAny(hasItems.WaitAsync(), Task.Delay(HalibutLimits.PollingQueueWaitTimeout));
            hasItems.Reset();
            return TakeFirst();
        }

        PendingRequest TakeFirst()
        {
            lock (sync)
            {
                if (queue.Count == 0)
                {
                    return null;
                }

                var first = queue[0];
                queue.RemoveAt(0);
                return first;
            }
        }

        public void ApplyResponse(ResponseMessage response, ServiceEndPoint destination)
        {
            if (response == null)
            {
                return;
            }

            lock (sync)
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
            readonly ManualResetEventSlim waiter;
            readonly object sync = new();
            bool transferBegun;
            bool completed;

            public PendingRequest(RequestMessage request, ILog log)
            {
                this.request = request;
                this.log = log;
                waiter = new ManualResetEventSlim(false);
            }

            public RequestMessage Request => request;

            public void WaitUntilComplete(CancellationToken cancellationToken)
            {
                log.Write(EventType.MessageExchange, "Request {0} was queued", request);

                bool success;
                var cancelled = false;

                try
                {
                    success = waiter.Wait(request.Destination.PollingRequestQueueTimeout, cancellationToken);
                    if (success)
                    {
                        log.Write(EventType.MessageExchange, "Request {0} was collected by the polling endpoint", request);
                        return;
                    }
                }
                catch (Exception ex) when (ex is TaskCanceledException or OperationCanceledException)
                {
                    // waiter.Set is only called when the request has been collected and the response received.
                    // It is possible that the transfer has already stated once the cancellationToken is cancelled
                    // In this case we cannot walk away from the request as it is already in progress and no longer in the connecting phase
                    cancelled = true;

                    lock (sync)
                    {
                        if (!transferBegun)
                        {
                            completed = true;
                            log.Write(EventType.MessageExchange, "Request {0} was cancelled before it could be collected by the polling endpoint", request);
                            throw;
                        }

                        log.Write(EventType.MessageExchange, "Request {0} was cancelled after it had been collected by the polling endpoint and will not be cancelled", request);
                    }
                }

                var waitForTransferToComplete = false;
                lock (sync)
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
                    success = waiter.Wait(request.Destination.PollingRequestMaximumMessageProcessingTimeout, CancellationToken.None);
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

            public bool BeginTransfer()
            {
                lock (sync)
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
                waiter.Set();
            }
        }
    }
}