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
using Halibut.ServiceModel;
using Halibut.Transport.Protocol;
using Halibut.Util;

namespace Halibut.Queue.Redis
{
    public class ProcessingNodeHeartBeatSender : IAsyncDisposable
    {
        public enum NodeProcessingRequestWatcherResult
        {
            ProcessingNodeIsLikelyDisconnected,
            NoIssue // TODO: this name is so bad the reviewer will be forced to think of a better one.
        }

        private readonly Uri endpoint;
        private readonly Guid requestActivityId; 
        private readonly HalibutRedisTransport halibutRedisTransport;
        private readonly CancellationTokenSource cancellationTokenSource;
        private readonly ILog log;
        
        public static readonly TimeSpan DefaultMaxTimeBetweenHeartBeetsBeforeProcessingNodeIsAssumedToBeOffline = TimeSpan.FromSeconds(60);
        

        public ProcessingNodeHeartBeatSender(Uri endpoint,
            Guid requestActivityId,
            HalibutRedisTransport halibutRedisTransport,
            ILog log)
        {
            this.endpoint = endpoint;
            this.requestActivityId = requestActivityId;
            this.halibutRedisTransport = halibutRedisTransport;
            this.cancellationTokenSource = new CancellationTokenSource();
            this.log = log;
            log.Write(EventType.Diagnostic, "Starting ProcessingNodeHeartBeatSender for request {0} to endpoint {1}", requestActivityId, endpoint);
            Task.Run(() => SendPulsesWhileProcessingRequest(cancellationTokenSource.Token));
        }

        private async Task SendPulsesWhileProcessingRequest(CancellationToken cancellationToken)
        {
            log.Write(EventType.Diagnostic, "Starting heartbeat pulse loop for request {0}", requestActivityId);
            
            TimeSpan delayBetweenPulse;
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await halibutRedisTransport.SendHeartBeatFromNodeProcessingTheRequest(endpoint, requestActivityId, cancellationToken);
                    delayBetweenPulse = TimeSpan.FromSeconds(15);
                    log.Write(EventType.Diagnostic, "Successfully sent heartbeat for request {0}, next pulse in {1} seconds", requestActivityId, delayBetweenPulse.TotalSeconds);
                }
                catch (Exception ex)
                {
                    if(cancellationToken.IsCancellationRequested) 
                    {
                        log.Write(EventType.Diagnostic, "Heartbeat pulse loop cancelled for request {0}", requestActivityId);
                        return;
                    }
                    // Panic send pulses.
                    delayBetweenPulse = TimeSpan.FromSeconds(7);
                    log.WriteException(EventType.Diagnostic, "Failed to send heartbeat for request {0}, switching to panic mode with {1} second intervals", ex, requestActivityId, delayBetweenPulse.TotalSeconds);
                }
                
                await Try.IgnoringError(async () => await Task.Delay(delayBetweenPulse, cancellationToken));
            }
            
            log.Write(EventType.Diagnostic, "Heartbeat pulse loop ended for request {0}", requestActivityId);
        }

        public static async Task<NodeProcessingRequestWatcherResult> WaitUntilNodeProcessingRequestFlatLines(
            Uri endpoint,
            RequestMessage request, 
            PendingRequest pending,
            HalibutRedisTransport halibutRedisTransport,
            ILog log,
            TimeSpan maxTimeBetweenHeartBeetsBeforeProcessingNodeIsAssumedToBeOffline,
            CancellationToken watchCancellationToken)
        {
            log.Write(EventType.Diagnostic, "Starting to watch for processing node flatline for request {0} to endpoint {1}", request.ActivityId, endpoint);
            
            // Once the pending's CT has been cancelled we no longer care to keep observing 
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(watchCancellationToken, pending.PendingRequestCancellationToken);

            DateTimeOffset? lastHeartBeat = null;
            
            try
            {
                await using var subscription = await halibutRedisTransport.SubscribeToNodeProcessingTheRequestHeartBeatChannel(
                    endpoint,
                    request.ActivityId, async () =>
                    {
                        await Task.CompletedTask;
                        lastHeartBeat = DateTimeOffset.Now;
                        log.Write(EventType.Diagnostic, "Received heartbeat for request {0} from processing node", request.ActivityId);
                    }, cts.Token);
                
                await WaitForRequestToBeCollected(endpoint, request, pending, halibutRedisTransport, log, cts.Token);

                // When the request was collected is a good enough heart beat.
                if (lastHeartBeat == null || lastHeartBeat.Value < DateTimeOffset.Now)
                {
                    lastHeartBeat = DateTimeOffset.Now;
                    log.Write(EventType.Diagnostic, "Using request collection time as heartbeat for request {0}", request.ActivityId);
                }
                
                while (!cts.Token.IsCancellationRequested)
                {
                    // TODO: I am sure a fancy pants delay could be done here calculated from the now and last heart beat etc.
                    await Try.IgnoringError(async () => await Task.Delay(TimeSpan.FromSeconds(10), cts.Token));
                    var timeSinceLastHeartBeat = DateTimeOffset.Now - lastHeartBeat.Value;
                    if (timeSinceLastHeartBeat > maxTimeBetweenHeartBeetsBeforeProcessingNodeIsAssumedToBeOffline)
                    {
                        log.Write(EventType.Diagnostic, "Processing node appears disconnected for request {0}, last heartbeat was {1} seconds ago", request.ActivityId, timeSinceLastHeartBeat.TotalSeconds);
                        return NodeProcessingRequestWatcherResult.ProcessingNodeIsLikelyDisconnected;
                    }
                }

                log.Write(EventType.Diagnostic, "Processing node watcher cancelled for request {0}", request.ActivityId);
                return NodeProcessingRequestWatcherResult.NoIssue;
            }
            catch (Exception ex)
            {
                log.WriteException(EventType.Diagnostic, "Error while watching processing node for request {0}", ex, request.ActivityId);
                throw;
            }
        }

        static async Task WaitForRequestToBeCollected(Uri endpoint, RequestMessage request, PendingRequest pending, HalibutRedisTransport halibutRedisTransport, ILog log, CancellationToken cancellationToken)
        {
            log.Write(EventType.Diagnostic, "Waiting for request {0} to be collected from queue", request.ActivityId);
            
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Has something else determined the request was collected?
                    // TODO should we bail out of here if the PendingRequest is complete? 
                    if(pending.HasRequestBeenMarkedAsCollected) 
                    {
                        log.Write(EventType.Diagnostic, "Request {0} has been marked as collected", request.ActivityId);
                        return;
                    }
                    

                    // So check ourselves if the request has been collected.
                    var requestIsStillOnQueue = await halibutRedisTransport.IsRequestStillOnQueue(endpoint, request.ActivityId, cancellationToken);
                    if(!requestIsStillOnQueue) 
                    {
                        log.Write(EventType.Diagnostic, "Request {0} is no longer on queue", request.ActivityId);
                        await pending.RequestHasBeenCollectedAndWillBeTransferred();
                        return;
                    }
                }
                catch (Exception ex)
                {
                    log.WriteException(EventType.Diagnostic, "Error checking if request {0} is still on queue", ex, request.ActivityId);
                }
                
                await Try.IgnoringError(async () => await Task.WhenAny(Task.Delay(TimeSpan.FromSeconds(30), cancellationToken), pending.WaitForRequestToBeMarkedAsCollected(cancellationToken)));
            }
            
            log.Write(EventType.Diagnostic, "Stopped waiting for request {0} to be collected (cancelled)", request.ActivityId);
        }

        public async ValueTask DisposeAsync()
        {
            log.Write(EventType.Diagnostic, "Disposing ProcessingNodeHeartBeatSender for request {0}", requestActivityId);
            
            await Try.IgnoringError(async () => await cancellationTokenSource.CancelAsync());
            Try.IgnoringError(() => cancellationTokenSource.Dispose());
            
            log.Write(EventType.Diagnostic, "ProcessingNodeHeartBeatSender disposed for request {0}", requestActivityId);
        }
    }
}