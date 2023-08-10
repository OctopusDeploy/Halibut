using System;

namespace Halibut.Tests.Support.ExtensionMethods
{
    public static class ServiceEndPointExtensionMethods
    {
        public static void TryAndConnectForALongTime(this ServiceEndPoint point)
        {
            point.RetryCountLimit = 1000000;
            point.ConnectionErrorRetryTimeout = TimeSpan.FromDays(1);
            point.PollingRequestQueueTimeout = TimeSpan.FromDays(1);
            point.TcpClientConnectTimeout = TimeSpan.FromDays(1);
        }
    }
}