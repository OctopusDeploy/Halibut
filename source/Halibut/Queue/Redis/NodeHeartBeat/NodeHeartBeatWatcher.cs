#if NET8_0_OR_GREATER
using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.Transport.Protocol;
using Halibut.Util;

namespace Halibut.Queue.Redis.NodeHeartBeat
{
    public class NodeHeartBeatWatcher
    {
        public static async Task<NodeWatcherResult> WatchThatNodeProcessingTheRequestIsStillAlive(
            Uri endpoint,
            RequestMessage request, 
            RedisPendingRequest redisPending,
            IHalibutRedisTransport halibutRedisTransport,
            TimeSpan timeBetweenCheckingIfRequestWasCollected,
            ILog log,
            TimeSpan maxTimeBetweenHeartBeetsBeforeProcessingNodeIsAssumedToBeOffline,
            CancellationToken watchCancellationToken)
        {
            log = log.ForContext<NodeHeartBeatWatcher>();
            // Once the pending's CT has been cancelled we no longer care to keep observing
            await using var cts = new CancelOnDisposeCancellationToken(watchCancellationToken, redisPending.PendingRequestCancellationToken);
            try
            {
                await WaitForRequestToBeCollected(endpoint, request, redisPending, halibutRedisTransport, timeBetweenCheckingIfRequestWasCollected, log, cts.Token);

                return await WatchForPulsesFromNode(endpoint, request.ActivityId, halibutRedisTransport, log, maxTimeBetweenHeartBeetsBeforeProcessingNodeIsAssumedToBeOffline, HalibutQueueNodeSendingPulses.RequestProcessorNode, cts.Token);
            }
            catch (Exception) when (cts.Token.IsCancellationRequested)
            {
                return NodeWatcherResult.NoDisconnectSeen;
            }
            catch (Exception)
            {
                return NodeWatcherResult.NodeMayHaveDisconnected;
            }
        }

        public static async Task<NodeWatcherResult> WatchThatNodeWhichSentTheRequestIsStillAlive(
            Uri endpoint,
            Guid requestActivityId,
            IHalibutRedisTransport halibutRedisTransport,
            ILog log,
            TimeSpan maxTimeBetweenSenderHeartBeetsBeforeSenderIsAssumedToBeOffline,
            CancellationToken watchCancellationToken)
        {
            try
            {
                return await WatchForPulsesFromNode(endpoint, requestActivityId, halibutRedisTransport, log, maxTimeBetweenSenderHeartBeetsBeforeSenderIsAssumedToBeOffline, HalibutQueueNodeSendingPulses.RequestSenderNode, watchCancellationToken);
            }
            catch (Exception) when (watchCancellationToken.IsCancellationRequested)
            {
                return NodeWatcherResult.NoDisconnectSeen;
            }
            catch (Exception)
            {
                return NodeWatcherResult.NodeMayHaveDisconnected;
            }
        }

        static async Task<NodeWatcherResult> WatchForPulsesFromNode(
            Uri endpoint,
            Guid requestActivityId, 
            IHalibutRedisTransport halibutRedisTransport,
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
                var subscriptionCts = new CancelOnDisposeCancellationToken(watchCancellationToken);
                
                // Subscribe
                var subscriptionTask =  Task.Run(async () => await halibutRedisTransport.SubscribeToNodeHeartBeatChannel(
                    endpoint,
                    requestActivityId,
                    watchingForPulsesFrom, 
                    async () =>
                    {
                        await Task.CompletedTask;
                        lastHeartBeat = DateTimeOffset.Now;
                        log.Write(EventType.Diagnostic, "Received heartbeat from {0} node, request {1}", watchingForPulsesFrom, requestActivityId);
                    }, subscriptionCts.Token));
                subscriptionCts.AwaitTasksBeforeCTSDispose(subscriptionTask);
                
                while (!watchCancellationToken.IsCancellationRequested)
                {
                    var timeSinceLastHeartBeat = DateTimeOffset.Now - lastHeartBeat.Value;
                    if (timeSinceLastHeartBeat > maxTimeBetweenHeartBeetsBeforeNodeIsAssumedToBeOffline)
                    {
                        log.Write(EventType.Diagnostic, "{0} node appears disconnected, request {1}, last heartbeat was {2} seconds ago", watchingForPulsesFrom, requestActivityId, timeSinceLastHeartBeat.TotalSeconds);
                        return NodeWatcherResult.NodeMayHaveDisconnected;
                    }
                    
                    var timeToWait = TimeSpanHelper.Min(
                        TimeSpan.FromSeconds(30), 
                        maxTimeBetweenHeartBeetsBeforeNodeIsAssumedToBeOffline - timeSinceLastHeartBeat + TimeSpan.FromSeconds(1)); 
                    
                    await Try.IgnoringError(async () => await Task.Delay(timeToWait, watchCancellationToken));
                }

                log.Write(EventType.Diagnostic, "{0} node watcher cancelled, request {1}", watchingForPulsesFrom, requestActivityId);
                return NodeWatcherResult.NoDisconnectSeen;
            }
            catch (Exception ex) when (!watchCancellationToken.IsCancellationRequested)
            {
                log.WriteException(EventType.Diagnostic, "Error while watching {0} node, request {1}", ex, watchingForPulsesFrom, requestActivityId);
                throw;
            }
        }

        static async Task WaitForRequestToBeCollected(
            Uri endpoint,
            RequestMessage request,
            RedisPendingRequest redisPending,
            IHalibutRedisTransport halibutRedisTransport,
            TimeSpan timeBetweenCheckingIfRequestWasCollected,
            ILog log,
            CancellationToken cancellationToken)
        {
            log = log.ForContext<NodeHeartBeatSender>();
            log.Write(EventType.Diagnostic, "Waiting for request {0} to be collected from queue", request.ActivityId);
            
            while (!cancellationToken.IsCancellationRequested)
            {
                await Try.IgnoringError(async () =>
                {
                    await Task.WhenAny(
                        Task.Delay(timeBetweenCheckingIfRequestWasCollected, cancellationToken),
                        redisPending.WaitForRequestToBeMarkedAsCollected(cancellationToken));
                });
                
                if(cancellationToken.IsCancellationRequested) break;
                
                try
                {
                    // Has something else determined the request was collected?
                    if (redisPending.HasRequestBeenMarkedAsCollected)
                    {
                        log.Write(EventType.Diagnostic, "Request {0} has been marked as collected", request.ActivityId);
                        return;
                    }

                    // Check ourselves if the request has been collected.
                    var requestIsStillOnQueue = await halibutRedisTransport.IsRequestStillOnQueue(endpoint, request.ActivityId, cancellationToken);
                    if (!requestIsStillOnQueue)
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
            }
            
            log.Write(EventType.Diagnostic, "Stopped waiting for request {0} to be collected (cancelled)", request.ActivityId);
        }
    }
}
#endif