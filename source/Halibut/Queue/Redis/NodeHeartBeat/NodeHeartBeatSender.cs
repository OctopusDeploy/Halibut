
#if NET8_0_OR_GREATER
using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.Util;

namespace Halibut.Queue.Redis.NodeHeartBeat
{
    public class NodeHeartBeatSender : IAsyncDisposable
    {
        readonly Uri endpoint;
        readonly Guid requestActivityId;
        readonly IHalibutRedisTransport halibutRedisTransport;
        readonly CancelOnDisposeCancellationToken cts;
        readonly ILog log;
        readonly HalibutQueueNodeSendingPulses nodeSendingPulsesType;

        internal Task TaskSendingPulses;
        public NodeHeartBeatSender(
            Uri endpoint,
            Guid requestActivityId,
            IHalibutRedisTransport halibutRedisTransport,
            ILog log,
            HalibutQueueNodeSendingPulses nodeSendingPulsesType,
            TimeSpan defaultDelayBetweenPulses)
        {
            this.endpoint = endpoint;
            this.requestActivityId = requestActivityId;
            this.halibutRedisTransport = halibutRedisTransport;
            this.nodeSendingPulsesType = nodeSendingPulsesType;
            cts = new CancelOnDisposeCancellationToken();
            this.log = log.ForContext<NodeHeartBeatSender>();
            this.log.Write(EventType.Diagnostic, "Starting NodeHeartBeatSender for {0} node, request {1}, endpoint {2}", nodeSendingPulsesType, requestActivityId, endpoint);
            TaskSendingPulses = Task.Run(() => SendPulsesWhileProcessingRequest(defaultDelayBetweenPulses, cts.Token));
        }

        async Task SendPulsesWhileProcessingRequest(TimeSpan defaultDelayBetweenPulses, CancellationToken cancellationToken)
        {
            log.Write(EventType.Diagnostic, "Starting heartbeat pulse loop for {0} node, request {1}", nodeSendingPulsesType, requestActivityId);
            
            TimeSpan delayBetweenPulse;
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await halibutRedisTransport.SendNodeHeartBeat(endpoint, requestActivityId, nodeSendingPulsesType, cancellationToken);
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
                    // Send pulses more frequently when we were unable to send a pulse.
                    delayBetweenPulse = defaultDelayBetweenPulses / 2;
                    log.WriteException(EventType.Diagnostic, "Failed to send heartbeat for {0} node, request {1}, switching to panic mode with {2} second intervals", ex, nodeSendingPulsesType, requestActivityId, delayBetweenPulse.TotalSeconds);
                }
                
                await Try.IgnoringError(async () => await Task.Delay(delayBetweenPulse, cancellationToken));
            }
            
            log.Write(EventType.Diagnostic, "Heartbeat pulse loop ended for {0} node, request {1}", nodeSendingPulsesType, requestActivityId);
        }

        public async ValueTask DisposeAsync()
        {
            log.Write(EventType.Diagnostic, "Disposing NodeHeartBeatSender for {0} node, request {1}", nodeSendingPulsesType, requestActivityId);
            
            await Try.IgnoringError(async () => await cts.DisposeAsync());
            
            log.Write(EventType.Diagnostic, "NodeHeartBeatSender disposed for {0} node, request {1}", nodeSendingPulsesType, requestActivityId);
        }
    }
}
#endif