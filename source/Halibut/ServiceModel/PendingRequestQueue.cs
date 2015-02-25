using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Halibut.Diagnostics;
using Halibut.Transport.Protocol;

namespace Halibut.ServiceModel
{
    public class PendingRequestQueue : IPendingRequestQueue
    {
        readonly List<PendingRequest> queue = new List<PendingRequest>();
        readonly Dictionary<string, PendingRequest> inProgress = new Dictionary<string, PendingRequest>();
        readonly object sync = new object();
        readonly ManualResetEventSlim hasItems = new ManualResetEventSlim();

        public ResponseMessage QueueAndWait(RequestMessage request)
        {
            var pending = new PendingRequest(request);

            lock (sync)
            {
                queue.Add(pending);
                inProgress.Add(request.Id, pending);
                hasItems.Set();
            }

            pending.WaitUntilComplete(HalibutLimits.MaximumTimeBeforeRequestsToPollingMachinesThatAreNotCollectedWillTimeOut);

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
            var pending = DequeueNext(TimeSpan.FromSeconds(30));
            if (pending == null) return null;
            return pending.BeginTransfer() ? pending.Request : null;
        }

        PendingRequest DequeueNext(TimeSpan maximumTimeToWaitForItem)
        {
            var first = TakeFirst();
            if (first != null)
                return first;

            hasItems.Wait(maximumTimeToWaitForItem);
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
            readonly ManualResetEventSlim waiter;
            readonly object sync = new object();
            bool transferBegun;
            bool completed;

            public PendingRequest(RequestMessage request)
            {
                this.request = request;
                waiter = new ManualResetEventSlim(false);
            }

            public RequestMessage Request
            {
                get { return request; }
            }

            public void WaitUntilComplete(TimeSpan maxTimeToWait)
            {
                var success = waiter.Wait(maxTimeToWait);
                if (success) 
                    return;

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
                    waiter.Wait();
                }
                else
                {
                    SetResponse(ResponseMessage.FromException(request, new TimeoutException(string.Format("A request was sent to a polling endpoint, but the polling endpoint did not collect the request within the allowed time ({0}), so the request timed out.", HalibutLimits.MaximumTimeBeforeRequestsToPollingMachinesThatAreNotCollectedWillTimeOut))));
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