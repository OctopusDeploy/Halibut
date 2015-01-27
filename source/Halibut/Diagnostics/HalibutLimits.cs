using System;

namespace Halibut.Diagnostics
{
    public class HalibutLimits
    {
        public static readonly TimeSpan MaximumTimeBeforeRequestsToPollingMachinesThatAreNotCollectedWillTimeOut = TimeSpan.FromSeconds(20);
        public static readonly TimeSpan TimeToSleepBetweenConnectionRetryAttemptsWhenCallingListeningEndpoint = TimeSpan.FromSeconds(1);
        public static readonly TimeSpan MaximumTimeToRetryAnyFormOfNetworkCommunicationWhenCallingListeningEndPoint = TimeSpan.FromSeconds(30);
        public static readonly TimeSpan TcpClientSendTimeout = TimeSpan.FromMinutes(5);
        public static readonly TimeSpan TcpClientReceiveTimeout = TimeSpan.FromMinutes(5);
        public static readonly TimeSpan TcpClientConnectTimeout = TimeSpan.FromSeconds(30);
    }
}