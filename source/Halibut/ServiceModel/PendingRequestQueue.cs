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
        readonly List<PendingRequest> queue = new List<PendingRequest>();
        readonly Dictionary<string, PendingRequest> inProgress = new Dictionary<string, PendingRequest>();
        readonly object sync = new object();
        readonly AsyncManualResetEvent hasItems = new AsyncManualResetEvent();
        readonly ILog log;

        public PendingRequestQueue(ILog log)
        {
            this.log = log;
        }

        public ResponseMessage QueueAndWait(RequestMessage request)
        {
            var pending = new PendingRequest(request, log);

            lock (sync)
            {
                queue.Add(pending);
                inProgress.Add(request.Id, pending);
                hasItems.Set();
            }

            pending.WaitUntilComplete();

            lock (sync)
            {
                inProgress.Remove(request.Id);
            }

            return pending.Response;
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
            if (pending == null) return null;
            return pending.BeginTransfer() ? pending.Request : null;
        }

        PendingRequest DequeueNext()
        {
            var first = TakeFirst();
            if (first != null)
                return first;

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
                return first;

            await Task.WhenAny(hasItems.WaitAsync(), Task.Delay(HalibutLimits.PollingQueueWaitTimeout));
            hasItems.Reset();
            return TakeFirst();
        }

        PendingRequest TakeFirst()
        {
            lock (sync)
            {
                if (queue.Count == 0)
                    return null;

                var first = queue[0];
                queue.RemoveAt(0);
                return first;
            }
        }

        public void ApplyResponse(ResponseMessage response)
        {
            if (response == null)
                return;

            lock (sync)
            {
                PendingRequest pending;
                if (inProgress.TryGetValue(response.Id, out pending))
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
            readonly object sync = new object();
            bool transferBegun;
            bool completed;

            public PendingRequest(RequestMessage request, ILog log)
            {
                this.request = request;
                this.log = log;
                waiter = new ManualResetEventSlim(false);
            }

            public RequestMessage Request
            {
                get { return request; }
            }

            public void WaitUntilComplete()
            {
                log.Write(EventType.MessageExchange, "Request {0} was queued", request);

                var success = waiter.Wait(request.Destination.PollingRequestQueueTimeout);
                if (success)
                {
                    log.Write(EventType.MessageExchange, "Request {0} was collected by the polling endpoint", request);
                    return;
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
                    success = waiter.Wait(request.Destination.PollingRequestMaximumMessageProcessingTimeout);
                    if (success)
                    {
                        log.Write(EventType.MessageExchange, "Request {0} was eventually collected by the polling endpoint", request);
                    }
                    else
                    {
                        SetResponse(ResponseMessage.FromException(request, new TimeoutException(string.Format("A request was sent to a polling endpoint, the polling endpoint collected it but did not respond in the allowed time ({0}), so the request timed out.", request.Destination.PollingRequestMaximumMessageProcessingTimeout))));
                    }
                }
                else
                {
                    log.Write(EventType.MessageExchange, "Request {0} timed out before it could be collected by the polling endpoint", request);
                    SetResponse(ResponseMessage.FromException(request, new TimeoutException(string.Format("A request was sent to a polling endpoint, but the polling endpoint did not collect the request within the allowed time ({0}), so the request timed out.", request.Destination.PollingRequestQueueTimeout))));
                }
            }

            public bool BeginTransfer()
            {
                lock (sync)
                {
                    if (completed)
                        return false;

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