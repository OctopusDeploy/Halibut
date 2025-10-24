#if NET8_0_OR_GREATER
using System;

namespace Halibut.Queue.Redis.NodeHeartBeat
{
    public enum NodeWatcherResult
    {
        NodeMayHaveDisconnected,
        NoDisconnectSeen
    }
}
#endif