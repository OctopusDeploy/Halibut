using System;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Halibut.Diagnostics;
using Halibut.Transport.Protocol;
using Halibut.Util.AsyncEx;

namespace Halibut.ServiceModel
{
    public class PendingRequestQueue : IPendingRequestQueue
    {
        readonly BufferBlock<PendingRequest> queue = new BufferBlock<PendingRequest>();

        readonly ILog log;

        public PendingRequestQueue(ILog log)
        {
            this.log = log;
        }

        public async Task<ResponseMessage> QueueAndWait(RequestMessage request)
        {
            var pending = new PendingRequest(request, log);

            await queue.SendAsync(pending).ConfigureAwait(false);

            await pending.WaitUntilComplete().ConfigureAwait(false);

            return pending.Response;
        }

        public Task<RequestMessage> DequeueAsync()
        {
            return queue.ReceiveAsync(HalibutLimits.PollingQueueWaitTimeout)
                .ContinueWith(task =>
                {
                    if (task.IsFaulted)
                    {
                        task.Exception.Handle(e => true);

                        return null;
                    }

                    if (task.IsCompleted)
                    {
                        if (task.Result.BeginTransfer())
                        {
                            return task.Result.Request;
                        }
                    }

                    return null;
                });
        }
        
        class PendingRequest
        {
            readonly RequestMessage request;
            readonly ILog log;
            readonly AsyncManualResetEvent waiter;
            bool transferBegun;
            bool completed;

            public PendingRequest(RequestMessage request, ILog log)
            {
                this.request = request;
                this.request.ResponseArrived = ResponseArrived;
                this.log = log;
                waiter = new AsyncManualResetEvent(false);
            }

            void ResponseArrived(ResponseMessage responseMessage)
            {
                SetResponse(responseMessage);
            }

            public RequestMessage Request => request;

            public async Task WaitUntilComplete()
            {
                log.Write(EventType.MessageExchange, "Request {0} was queued", request);

                var timeoutTask = Task.Delay(HalibutLimits.PollingRequestQueueTimeout);
                var success = await Task.WhenAny(waiter.WaitAsync(), timeoutTask)
                    .ContinueWith(t =>
                    {
                        var finishedTask = t.Result;
                        return !finishedTask.Equals(timeoutTask);
                    })
                    .ConfigureAwait(false);

                if (success)
                {
                    log.Write(EventType.MessageExchange, "Request {0} was collected by the polling endpoint", request);
                    return;
                }

                if (transferBegun)
                {
                    timeoutTask = Task.Delay(HalibutLimits.PollingRequestMaximumMessageProcessingTimeout);
                    success = await Task.WhenAny(waiter.WaitAsync(), timeoutTask)
                        .ContinueWith(t =>
                        {
                            var finishedTask = t.Result;
                            return !finishedTask.Equals(timeoutTask);
                        })
                        .ConfigureAwait(false);

                    if (success)
                    {
                        log.Write(EventType.MessageExchange, "Request {0} was eventually collected by the polling endpoint", request);
                    }
                    else
                    {
                        SetResponse(ResponseMessage.FromException(request, new TimeoutException(string.Format("A request was sent to a polling endpoint, the polling endpoint collected it but did not respond in the allowed time ({0}), so the request timed out.", HalibutLimits.PollingRequestMaximumMessageProcessingTimeout))));
                    }
                }
                else
                {
                    completed = true;

                    log.Write(EventType.MessageExchange, "Request {0} timed out before it could be collected by the polling endpoint", request);
                    SetResponse(ResponseMessage.FromException(request, new TimeoutException(string.Format("A request was sent to a polling endpoint, but the polling endpoint did not collect the request within the allowed time ({0}), so the request timed out.", HalibutLimits.PollingRequestQueueTimeout))));
                }
            }

            public ResponseMessage Response { get; private set; }

            void SetResponse(ResponseMessage response)
            {
                Response = response;
                waiter.Set();
            }

            public bool BeginTransfer()
            {
                if (completed)
                {
                    return false;
                }

                transferBegun = true;

                return true;
            }
        }
    }
}