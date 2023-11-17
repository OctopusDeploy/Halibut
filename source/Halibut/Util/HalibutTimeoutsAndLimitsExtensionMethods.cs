using System;
using Halibut.Diagnostics;

namespace Halibut.Util
{
    public static class HalibutTimeoutsAndLimitsExtensionMethods
    {
        public static HalibutTimeoutsAndLimits WithTcpClientReceiveTimeout(this HalibutTimeoutsAndLimits halibutTimeoutsAndLimits, TimeSpan tcpClientReceiveTimeout)
        {
            halibutTimeoutsAndLimits.TcpClientTimeout = new(
                sendTimeout: halibutTimeoutsAndLimits.TcpClientTimeout.SendTimeout, 
                receiveTimeout: tcpClientReceiveTimeout);

            return halibutTimeoutsAndLimits;
        }
    }
}