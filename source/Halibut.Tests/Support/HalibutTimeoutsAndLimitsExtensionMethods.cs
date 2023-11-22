using System;
using Halibut.Diagnostics;

namespace Halibut.Tests.Support
{
    public static class HalibutTimeoutsAndLimitsExtensionMethods
    {
        public static HalibutTimeoutsAndLimits WithAllTcpTimeoutsTo(this HalibutTimeoutsAndLimits halibutTimeoutsAndLimits, TimeSpan timeSpan)
        {
            halibutTimeoutsAndLimits.TcpClientConnectTimeout = timeSpan;
            halibutTimeoutsAndLimits.TcpClientTimeout = new(timeSpan, timeSpan);
            halibutTimeoutsAndLimits.TcpClientHeartbeatTimeout  = new(timeSpan, timeSpan);
            halibutTimeoutsAndLimits.TcpClientReceiveResponseTimeout = timeSpan;
            halibutTimeoutsAndLimits.TcpClientReceiveResponseTransmissionAfterInitialReadTimeout = timeSpan;
            halibutTimeoutsAndLimits.TcpClientAuthenticationAndIdentificationTimeouts = new(timeSpan, timeSpan);

            return halibutTimeoutsAndLimits;
        }

        public static HalibutTimeoutsAndLimits WithTcpClientReceiveTimeout(this HalibutTimeoutsAndLimits halibutTimeoutsAndLimits, TimeSpan tcpClientReceiveTimeout)
        {
            halibutTimeoutsAndLimits.TcpClientReceiveResponseTimeout = tcpClientReceiveTimeout;

            return halibutTimeoutsAndLimits;
        }
    }
}