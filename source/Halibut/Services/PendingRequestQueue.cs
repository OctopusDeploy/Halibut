using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Halibut.Protocol;

namespace Halibut.Services
{
    public class PendingRequestQueue : IPendingRequestQueue
    {
        readonly ConcurrentDictionary<string, PendingRequest> requests = new ConcurrentDictionary<string, PendingRequest>();
        readonly ConcurrentQueue<RequestMessage> outgoing = new ConcurrentQueue<RequestMessage>();

        public ResponseMessage QueueAndWait(RequestMessage request)
        {
            var pending = new PendingRequest();
            requests.TryAdd(request.Id, pending);
            outgoing.Enqueue(request);
            var success = pending.Wait(TimeSpan.FromSeconds(20));
            if (!success)
            {
                ApplyResponse(ResponseMessage.FromException(request, new TimeoutException("A request was sent to a polling endpoint, but the polling endpoint did not collect the request within the allowed time (20 seconds), so the request timed out.")));
            }

            return pending.Response;
        }

        public bool IsEmpty { get { return outgoing.IsEmpty; } }

        public RequestMessage Dequeue()
        {
            RequestMessage result;
            outgoing.TryDequeue(out result);
            return result;
        }

        public void ApplyResponse(ResponseMessage response)
        {
            if (response == null)
                return;

            PendingRequest pending;
            if (!requests.TryRemove(response.Id, out pending))
            {
                throw new Exception("Must have died, message cannot be accepted");
            }

            pending.SetResponse(response);
        }

        class PendingRequest
        {
            readonly ManualResetEventSlim waiter;

            public PendingRequest()
            {
                waiter = new ManualResetEventSlim(false);
            }

            public bool Wait(TimeSpan maxTimeToWait)
            {
                return waiter.Wait(maxTimeToWait);
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