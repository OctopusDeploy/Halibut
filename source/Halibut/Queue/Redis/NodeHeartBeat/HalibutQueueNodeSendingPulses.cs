#if NET8_0_OR_GREATER
using System;

namespace Halibut.Queue.Redis.NodeHeartBeat
{
    public enum HalibutQueueNodeSendingPulses
    {
        // The node the RPC is executing on.
        // The node that calls QueueAndWait
        RequestSenderNode,
        
        // The node sending/receiving the Request to/from the service.
        // The node that calls Dequeue and ApplyResponse.
        RequestProcessorNode
    }
}
#endif