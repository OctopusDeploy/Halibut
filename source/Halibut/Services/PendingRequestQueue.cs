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
            pending.Wait();
            return pending.Response;
        }

        public bool IsEmpty { get { return outgoing.IsEmpty; } }

        public List<RequestMessage> DequeueRequests()
        {
            var results = new List<RequestMessage>();
            RequestMessage request;
            while (outgoing.TryDequeue(out request))
            {
                results.Add(request);
            }
            return results;
        }

        public void ApplyResponses(List<ResponseMessage> responses)
        {
            foreach (var response in responses)
            {
                PendingRequest pending;
                if (!requests.TryRemove(response.Id, out pending))
                {
                    throw new Exception("Must have died, message cannot be accepted");
                }

                pending.SetResponse(response);
            }
        }

        class PendingRequest
        {
            readonly ManualResetEventSlim waiter;

            public PendingRequest()
            {
                waiter = new ManualResetEventSlim(false);
            }

            public void Wait()
            {
                waiter.Wait();
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