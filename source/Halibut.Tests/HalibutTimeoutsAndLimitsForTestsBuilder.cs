using System;
using Halibut.Diagnostics;

namespace Halibut.Tests
{
    public class HalibutTimeoutsAndLimitsForTestsBuilder
    {
        public static readonly TimeSpan HalfTheTcpReceiveTimeout = TimeSpan.FromSeconds(22.5);
        static readonly TimeSpan TcpReceiveTimeout = HalfTheTcpReceiveTimeout + HalfTheTcpReceiveTimeout;

        readonly HalibutTimeoutsAndLimitsBuilder builder = new HalibutTimeoutsAndLimitsBuilder()
            .WithOverride(
                // The following 4 can be overriden, so set them high and let the test author drop the values as needed.
                // Also set to a "weird value" to make it more obvious which timeout is at play in tests.
                pollingRequestQueueTimeout: TimeSpan.FromSeconds(66),
                pollingRequestMaximumMessageProcessingTimeout: TimeSpan.FromSeconds(66),
                retryListeningSleepInterval: TimeSpan.FromSeconds(1),
                connectionErrorRetryTimeout: TimeSpan.FromSeconds(66), // Must always be greater than the heartbeat timeout.

                // Intentionally set higher than the heart beat, since some tests need to determine that the hart beat timeout applies.
                tcpClientTimeout: new(
                    sendTimeout: TcpReceiveTimeout,
                    receiveTimeout: TcpReceiveTimeout),
                tcpListeningNextRequestIdleTimeout: TcpReceiveTimeout,

                tcpClientReceiveResponseTimeout: TcpReceiveTimeout,

                tcpClientHeartbeatTimeout: new(
                    sendTimeout: TimeSpan.FromSeconds(15),
                    receiveTimeout: TimeSpan.FromSeconds(15)),

                tcpClientConnectTimeout: TimeSpan.FromSeconds(20),
                pollingQueueWaitTimeout: TimeSpan.FromSeconds(20),
                tcpClientReceiveRequestTimeoutForPolling: TimeSpan.FromSeconds(20) + TimeSpan.FromSeconds(10),
                tcpClientHeartbeatTimeoutShouldActuallyBeUsed: true
            );

        // The following 4 can be overriden, so set them high and let the test author drop the values as needed.
        // Also set to a "weird value" to make it more obvious which timeout is at play in tests.
        // Must always be greater than the heartbeat timeout.
        // Intentionally set higher than the heart beat, since some tests need to determine that the hart beat timeout applies.
        public HalibutTimeoutsAndLimitsForTestsBuilder WithAllTcpTimeoutsTo(TimeSpan timeSpan)
        {
            builder.WithOverride(
                tcpClientConnectTimeout: timeSpan,
                tcpClientTimeout: new(timeSpan, timeSpan),
                tcpClientHeartbeatTimeout: new(timeSpan, timeSpan),
                tcpClientReceiveResponseTimeout: timeSpan,
                tcpListeningNextRequestIdleTimeout: timeSpan,
                tcpClientReceiveRequestTimeoutForPolling: timeSpan);
            return this;
        }

        public HalibutTimeoutsAndLimitsForTestsBuilder WithTcpClientReceiveTimeout(TimeSpan tcpClientReceiveTimeout)
        {
            builder.WithOverride(tcpClientReceiveResponseTimeout: tcpClientReceiveTimeout);
            return this;
        }
        
        public HalibutTimeoutsAndLimitsForTestsBuilder WithTcpClientTimeout(SendReceiveTimeout tcpClientTimeout)
        {
            builder.WithOverride(tcpClientTimeout: tcpClientTimeout);
            return this;
        }

        public HalibutTimeoutsAndLimitsForTestsBuilder WithTcpKeepAliveEnabled(bool tcpKeepAliveEnabled)
        {
            builder.WithTcpKeepAliveEnabled(tcpKeepAliveEnabled);
            return this;
        }

        public HalibutTimeoutsAndLimitsForTestsBuilder WithAdjustments(Action<HalibutTimeoutsAndLimitsBuilder> adjustment)
        {
            adjustment(builder);
            return this;
        }

        public HalibutTimeoutsAndLimits Build()
        {
            return builder.Build();
        }
    }
}