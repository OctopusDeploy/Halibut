using System;
using Halibut.Diagnostics;

namespace Halibut.Tests.Support
{
    public static class HalibutTimeoutsAndLimitsExtensionMethods
    {
        public static HalibutTimeoutsAndLimits SetAllTcpTimeoutsTo(this HalibutTimeoutsAndLimits halibutTimeoutsAndLimits, TimeSpan timeSpan)
        {
            halibutTimeoutsAndLimits.TcpClientConnectTimeout = timeSpan;
            
            halibutTimeoutsAndLimits.TcpClientReceiveTimeout = timeSpan;
            halibutTimeoutsAndLimits.TcpClientSendTimeout = timeSpan;
            
            halibutTimeoutsAndLimits.TcpClientHeartbeatReceiveTimeout = timeSpan;
            halibutTimeoutsAndLimits.TcpClientHeartbeatSendTimeout = timeSpan;
            return halibutTimeoutsAndLimits;
        }
    }
}