// Copyright 2012-2013 Octopus Deploy Pty. Ltd.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//   http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.Transport.Protocol;

namespace Halibut.Queue.Redis
{
    // TODO this code is mostly generic, so it could be shared
    public class RedisPendingRequest
    {
        readonly RequestMessage request;
        readonly ILog log;
        readonly ManualResetEventSlim responseRecievedEvent;
        readonly object sync = new();
        bool requestCollected;
        bool completed;

        readonly TimeSpan pollingRequestMaximumMessageProcessingTimeout;

        public RedisPendingRequest(RequestMessage request, ILog log, TimeSpan pollingRequestMaximumMessageProcessingTimeout)
        {
            this.request = request;
            this.log = log;
            this.pollingRequestMaximumMessageProcessingTimeout = pollingRequestMaximumMessageProcessingTimeout;
            responseRecievedEvent = new ManualResetEventSlim(false);
        }

        // Waits for the request to be collected and response to come back
        public async Task WaitUntilComplete(CancellationToken cancellationToken, Func<Task> pollingRequestQueueTimeElapsed)
        {
            await Task.CompletedTask;
            log.Write(EventType.MessageExchange, "Request {0} was queued", request);

            var success = responseRecievedEvent.Wait(request.Destination.PollingRequestQueueTimeout, cancellationToken);
            if (success)
            {
                log.Write(EventType.MessageExchange, "Request {0} was collected by the polling endpoint", request);
                return;
            }

            try
            {
                // Let em know the amount of time the request can sit on the queue for has elapsed. 
                await pollingRequestQueueTimeElapsed();
            }
            catch
            {
            }
            var waitForTransferToComplete = false;
            lock (sync)
            {
                if (requestCollected)
                    waitForTransferToComplete = true;
                else
                    completed = true;
            }

            if (waitForTransferToComplete)
            {
                success = responseRecievedEvent.Wait(pollingRequestMaximumMessageProcessingTimeout);
                if (success)
                    log.Write(EventType.MessageExchange, "Request {0} was eventually collected by the polling endpoint", request);
                else
                    SetResponse(ResponseMessage.FromException(request, new TimeoutException(string.Format("A request was sent to a polling endpoint, the polling endpoint collected it but did not respond in the allowed time ({0}), so the request timed out.", pollingRequestMaximumMessageProcessingTimeout))));
            }
            else
            {
                log.Write(EventType.MessageExchange, "Request {0} timed out before it could be collected by the polling endpoint", request);
                SetResponse(ResponseMessage.FromException(request, new TimeoutException(string.Format("A request was sent to a polling endpoint, but the polling endpoint did not collect the request within the allowed time ({0}), so the request timed out.", request.Destination.PollingRequestQueueTimeout))));
            }
        }

        public bool FYITheRequestHasBeenCollected()
        {
            lock (sync)
            {
                if (completed)
                    return false;

                requestCollected = true;
                return true;
            }
        }

        public ResponseMessage? Response { get; private set; }

        public void SetResponse(ResponseMessage response)
        {
            lock (sync)
            {
                if (Response == null)
                {
                    Response = response;
                    responseRecievedEvent.Set();
                }
            }
        }
    }
}