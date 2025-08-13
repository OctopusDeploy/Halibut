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
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.ServiceModel;
using Halibut.Transport.Protocol;
using Halibut.Util;
using Nito.AsyncEx;

namespace Halibut.Queue.Redis
{
    public enum HalibutQueueNodeSendingPulses
    {
        Sender,
        Receiver
    }
    public class NodeHeartBeatSender : IAsyncDisposable
    {
        public enum NodeProcessingRequestWatcherResult
        {
            NodeMayHaveDisconnected,
            NoDisconnectSeen
        }
        
        private readonly Uri endpoint;
        private readonly Guid requestActivityId; 
        private readonly HalibutRedisTransport halibutRedisTransport;
        private readonly CancelOnDisposeCancellationToken cancellationToken;
        private readonly ILog log;
        private readonly HalibutQueueNodeSendingPulses nodeSendingPulsesType;

        internal Task TaskSendingPulses;
        public NodeHeartBeatSender(
            Uri endpoint,
            Guid requestActivityId,
            HalibutRedisTransport halibutRedisTransport,
            ILog log,
            HalibutQueueNodeSendingPulses nodeSendingPulsesType,
            TimeSpan defaultDelayBetweenPulses)
        {
            this.endpoint = endpoint;
            this.requestActivityId = requestActivityId;
            this.halibutRedisTransport = halibutRedisTransport;
            this.nodeSendingPulsesType = nodeSendingPulsesType;
            cancellationToken = new CancelOnDisposeCancellationToken();
            this.log = log.ForContext<NodeHeartBeatSender>();
            this.log.Write(EventType.Diagnostic, "Starting NodeHeartBeatSender for {0} node, request {1}, endpoint {2}", nodeSendingPulsesType, requestActivityId, endpoint);
            TaskSendingPulses = Task.Run(() => SendPulsesWhileProcessingRequest(defaultDelayBetweenPulses, cancellationToken.Token));
        }

        private async Task SendPulsesWhileProcessingRequest(TimeSpan defaultDelayBetweenPulses, CancellationToken cancellationToken)
        {
            log.Write(EventType.Diagnostic, "Starting heartbeat pulse loop for {0} node, request {1}", nodeSendingPulsesType, requestActivityId);
            
            TimeSpan delayBetweenPulse;
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await halibutRedisTransport.SendHeartBeatFromNodeProcessingTheRequest(endpoint, requestActivityId, nodeSendingPulsesType, cancellationToken);
                    delayBetweenPulse = defaultDelayBetweenPulses;
                    log.Write(EventType.Diagnostic, "Successfully sent heartbeat for {0} node, request {1}, next pulse in {2} seconds", nodeSendingPulsesType, requestActivityId, delayBetweenPulse.TotalSeconds);
                }
                catch (Exception ex)
                {
                    if(cancellationToken.IsCancellationRequested) 
                    {
                        log.Write(EventType.Diagnostic, "Heartbeat pulse loop cancelled for {0} node, request {1}", nodeSendingPulsesType, requestActivityId);
                        return;
                    }
                    // Panic send pulses.
                    delayBetweenPulse = defaultDelayBetweenPulses / 2;
                    log.WriteException(EventType.Diagnostic, "Failed to send heartbeat for {0} node, request {1}, switching to panic mode with {2} second intervals", ex, nodeSendingPulsesType, requestActivityId, delayBetweenPulse.TotalSeconds);
                }
                
                await Try.IgnoringError(async () => await Task.Delay(delayBetweenPulse, cancellationToken));
            }
            
            log.Write(EventType.Diagnostic, "Heartbeat pulse loop ended for {0} node, request {1}", nodeSendingPulsesType, requestActivityId);
        }

        public static async Task<NodeProcessingRequestWatcherResult> WatchThatNodeProcessingTheRequestIsStillAlive(
            Uri endpoint,
            RequestMessage request, 
            RedisPendingRequest redisPending,
            HalibutRedisTransport halibutRedisTransport,
            TimeSpan timeBetweenCheckingIfRequestWasCollected,
            ILog log,
            TimeSpan maxTimeBetweenHeartBeetsBeforeProcessingNodeIsAssumedToBeOffline,
            CancellationToken watchCancellationToken)
        {
            log = log.ForContext<NodeHeartBeatSender>();
            // Once the pending's CT has been cancelled we no longer care to keep observing
            await using var cts = new CancelOnDisposeCancellationToken(watchCancellationToken, redisPending.PendingRequestCancellationToken);
            try
            {
                // TODO: test this is indeed called first.
                await WaitForRequestToBeCollected(endpoint, request, redisPending, halibutRedisTransport, timeBetweenCheckingIfRequestWasCollected, log, cts.Token);

                return await WatchForPulsesFromNode(endpoint, request.ActivityId, halibutRedisTransport, log, maxTimeBetweenHeartBeetsBeforeProcessingNodeIsAssumedToBeOffline, HalibutQueueNodeSendingPulses.Receiver, cts.Token);
            }
            catch (Exception) when (!cts.Token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                return NodeProcessingRequestWatcherResult.NoDisconnectSeen;
            }
        }

        public static async Task<NodeProcessingRequestWatcherResult> WatchThatNodeWhichSentTheRequestIsStillAlive(
            Uri endpoint,
            Guid requestActivityId,
            HalibutRedisTransport halibutRedisTransport,
            ILog log,
            TimeSpan maxTimeBetweenSenderHeartBeetsBeforeSenderIsAssumedToBeOffline,
            CancellationToken watchCancellationToken)
        {
            return await WatchForPulsesFromNode(endpoint, requestActivityId, halibutRedisTransport, log, maxTimeBetweenSenderHeartBeetsBeforeSenderIsAssumedToBeOffline, HalibutQueueNodeSendingPulses.Sender, watchCancellationToken);
        }

        private static async Task<NodeProcessingRequestWatcherResult> WatchForPulsesFromNode(
            Uri endpoint,
            Guid requestActivityId, 
            HalibutRedisTransport halibutRedisTransport,
            ILog log,
            TimeSpan maxTimeBetweenHeartBeetsBeforeNodeIsAssumedToBeOffline,
            HalibutQueueNodeSendingPulses watchingForPulsesFrom,
            CancellationToken watchCancellationToken)
        {
            log.ForContext<NodeHeartBeatSender>();
            log.Write(EventType.Diagnostic, "Starting to watch for pulses from {0} node, request {1}, endpoint {2}", watchingForPulsesFrom, requestActivityId, endpoint);

            DateTimeOffset? lastHeartBeat = DateTimeOffset.Now;
            
            try
            {
                await using var subscription = await halibutRedisTransport.SubscribeToNodeHeartBeatChannel(
                    endpoint,
                    requestActivityId,
                    watchingForPulsesFrom, 
                    async () =>
                    {
                        await Task.CompletedTask;
                        lastHeartBeat = DateTimeOffset.Now;
                        log.Write(EventType.Diagnostic, "Received heartbeat from {0} node, request {1}", watchingForPulsesFrom, requestActivityId);
                    }, watchCancellationToken);
                
                while (!watchCancellationToken.IsCancellationRequested)
                {
                    
                    var timeSinceLastHeartBeat = DateTimeOffset.Now - lastHeartBeat.Value;
                    if (timeSinceLastHeartBeat > maxTimeBetweenHeartBeetsBeforeNodeIsAssumedToBeOffline)
                    {
                        log.Write(EventType.Diagnostic, "{0} node appears disconnected, request {1}, last heartbeat was {2} seconds ago", watchingForPulsesFrom, requestActivityId, timeSinceLastHeartBeat.TotalSeconds);
                        return NodeProcessingRequestWatcherResult.NodeMayHaveDisconnected;
                    }

                    var timeToWait = TimeSpan.FromSeconds(30);
                    var timeBeforeTimeoutPlusOneSecond = maxTimeBetweenHeartBeetsBeforeNodeIsAssumedToBeOffline - timeSinceLastHeartBeat + TimeSpan.FromSeconds(1);
                    if (timeBeforeTimeoutPlusOneSecond < timeToWait) timeToWait = timeBeforeTimeoutPlusOneSecond;
                    
                    await Try.IgnoringError(async () => await Task.Delay(timeToWait, watchCancellationToken));
                }

                log.Write(EventType.Diagnostic, "{0} node watcher cancelled, request {1}", watchingForPulsesFrom, requestActivityId);
                return NodeProcessingRequestWatcherResult.NoDisconnectSeen;
            }
            catch (Exception ex) when (!watchCancellationToken.IsCancellationRequested)
            {
                log.WriteException(EventType.Diagnostic, "Error while watching {0} node, request {1}", ex, watchingForPulsesFrom, requestActivityId);
                throw;
            }
        }
        
        static async Task WaitForRequestToBeCollected(Uri endpoint, RequestMessage request, RedisPendingRequest redisPending, HalibutRedisTransport halibutRedisTransport,
            TimeSpan timeBetweenCheckingIfRequestWasCollected,
            ILog log, CancellationToken cancellationToken)
        {
            log = log.ForContext<NodeHeartBeatSender>();
            log.Write(EventType.Diagnostic, "Waiting for request {0} to be collected from queue", request.ActivityId);

            // TODO: Is this worthwhile?
            var asyncManualResetEvent = new AsyncManualResetEvent(false);
            // await using var subscription = await halibutRedisTransport.SubscribeToNodeHeartBeatChannel(
            //     endpoint,
            //     request.ActivityId,
            //     HalibutQueueNodeSendingPulses.Receiver, async () =>
            //     {
            //         await Task.CompletedTask;
            //         asyncManualResetEvent.Set();
            //         log.Write(EventType.Diagnostic, "While waiting for request to be collected received heartbeat from {0} node, request {1}", HalibutQueueNodeSendingPulses.Receiver, request.ActivityId);
            //     }, cancellationToken);
            
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    asyncManualResetEvent.Reset();
                    // Has something else determined the request was collected?
                    if(redisPending.HasRequestBeenMarkedAsCollected) 
                    {
                        log.Write(EventType.Diagnostic, "Request {0} has been marked as collected", request.ActivityId);
                        return;
                    }
                    

                    // So check ourselves if the request has been collected.
                    var requestIsStillOnQueue = await halibutRedisTransport.IsRequestStillOnQueue(endpoint, request.ActivityId, cancellationToken);
                    if(!requestIsStillOnQueue) 
                    {
                        log.Write(EventType.Diagnostic, "Request {0} is no longer on queue", request.ActivityId);
                        await redisPending.RequestHasBeenCollectedAndWillBeTransferred();
                        return;
                    }
                }
                catch (Exception ex)
                {
                    log.WriteException(EventType.Diagnostic, "Error checking if request {0} is still on queue", ex, request.ActivityId);
                }
                
                await Try.IgnoringError(async () =>
                {
                    await Task.WhenAny(
                        Task.Delay(timeBetweenCheckingIfRequestWasCollected, cancellationToken),
                        asyncManualResetEvent.WaitAsync(cancellationToken),
                        redisPending.WaitForRequestToBeMarkedAsCollected(cancellationToken));
                });
            }
            
            log.Write(EventType.Diagnostic, "Stopped waiting for request {0} to be collected (cancelled)", request.ActivityId);
        }

        public async ValueTask DisposeAsync()
        {
            log.Write(EventType.Diagnostic, "Disposing NodeHeartBeatSender for {0} node, request {1}", nodeSendingPulsesType, requestActivityId);
            
            await Try.IgnoringError(async () => await cancellationToken.DisposeAsync());
            
            log.Write(EventType.Diagnostic, "NodeHeartBeatSender disposed for {0} node, request {1}", nodeSendingPulsesType, requestActivityId);
        }
    }
}