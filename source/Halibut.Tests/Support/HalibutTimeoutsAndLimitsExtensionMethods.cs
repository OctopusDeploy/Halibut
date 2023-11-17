using System;
using Halibut.Diagnostics;

namespace Halibut.Tests.Support
{
    public static class HalibutTimeoutsAndLimitsExtensionMethods
    {
        public static HalibutTimeoutsAndLimits SetAllTcpTimeoutsTo(this HalibutTimeoutsAndLimits halibutTimeoutsAndLimits, TimeSpan timeSpan)
        {
            halibutTimeoutsAndLimits.TcpClientConnectTimeout = timeSpan;
            halibutTimeoutsAndLimits.TcpClientTimeout = new(timeSpan, timeSpan);
            halibutTimeoutsAndLimits.TcpClientHeartbeatTimeout  = new(timeSpan, timeSpan);

            return halibutTimeoutsAndLimits;
        }
    }
}