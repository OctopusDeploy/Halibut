using System;
using Halibut.Diagnostics;

namespace Halibut.Tests
{
    public class HalibutTimeoutsAndLimitsForTestsBuilder
    {
        public static readonly TimeSpan HalfTheTcpReceiveTimeout = TimeSpan.FromSeconds(22.5);

        public HalibutTimeoutsAndLimits Build()
        {
            return new HalibutTimeoutsAndLimits
            {
                // The following 4 can be overriden, so set them high and let the test author drop the values as needed.
                // Also set to a "weird value" to make it more obvious which timeout is at play in tests.
                PollingRequestQueueTimeout = TimeSpan.FromSeconds(66),
                PollingRequestMaximumMessageProcessingTimeout = TimeSpan.FromSeconds(66),
                RetryListeningSleepInterval = TimeSpan.FromSeconds(1),
                ConnectionErrorRetryTimeout = TimeSpan.FromSeconds(66), // Must always be greater than the heartbeat timeout.
            
                // Intentionally set higher than the heart beat, since some tests need to determine that the hart beat timeout applies.
                TcpClientTimeout = new(
                    sendTimeout: HalfTheTcpReceiveTimeout + HalfTheTcpReceiveTimeout, 
                    receiveTimeout: HalfTheTcpReceiveTimeout + HalfTheTcpReceiveTimeout),
            
                TcpClientHeartbeatTimeout = new(
                    sendTimeout: TimeSpan.FromSeconds(15), 
                    receiveTimeout: TimeSpan.FromSeconds(15)),
                
                TcpClientConnectTimeout = TimeSpan.FromSeconds(20),
                PollingQueueWaitTimeout = TimeSpan.FromSeconds(20)
            };
        }
    }
}