using System;

namespace Halibut.Diagnostics
{
    public class HalibutTimeoutsAndLimitsBuilder
    {
        TimeSpan pollingRequestQueueTimeout = TimeSpan.FromMinutes(2);
        TimeSpan pollingRequestMaximumMessageProcessingTimeout = TimeSpan.FromMinutes(10);
        TimeSpan retryListeningSleepInterval = TimeSpan.FromSeconds(1);
        int retryCountLimit = 5;
        TimeSpan connectionErrorRetryTimeout = TimeSpan.FromMinutes(5);
        int rewindableBufferStreamSize = 8192;
        SendReceiveTimeout tcpClientTimeout = new(sendTimeout: TimeSpan.FromMinutes(10), receiveTimeout: TimeSpan.FromMinutes(10));
        TimeSpan tcpClientReceiveResponseTimeout = TimeSpan.FromMinutes(10);
        TimeSpan tcpClientReceiveRequestTimeoutForPolling = TimeSpan.FromMinutes(10);
        TimeSpan tcpClientPooledConnectionTimeout = TimeSpan.FromMinutes(9);
        TimeSpan tcpListeningNextRequestIdleTimeout = TimeSpan.FromMinutes(10);
        SendReceiveTimeout tcpClientHeartbeatTimeout = new(sendTimeout: TimeSpan.FromSeconds(60), receiveTimeout: TimeSpan.FromSeconds(60));
        bool tcpClientHeartbeatTimeoutShouldActuallyBeUsed = false;
        TimeSpan tcpClientConnectTimeout = TimeSpan.FromSeconds(60);
        TimeSpan pollingQueueWaitTimeout = TimeSpan.FromSeconds(30);
        bool tcpKeepAliveEnabled = true;
        int tcpKeepAliveRetryCount = 10;
        TimeSpan tcpKeepAliveTime = TimeSpan.FromSeconds(15);
        TimeSpan tcpKeepAliveInterval = TimeSpan.FromSeconds(5);


        /// <summary>
        /// In the future these will become the default
        /// </summary>
        public HalibutTimeoutsAndLimitsBuilder WithRecommendedValues()
        {
            // In general all writes/read calls should take less than a minute.
            tcpClientTimeout = new(sendTimeout: TimeSpan.FromMinutes(1), receiveTimeout: TimeSpan.FromMinutes(1));
            tcpClientReceiveResponseTimeout = TimeSpan.FromMinutes(5); // ~ 5 minutes to execute a RPC call
            tcpClientReceiveRequestTimeoutForPolling = new HalibutTimeoutsAndLimitsBuilder().pollingQueueWaitTimeout + TimeSpan.FromSeconds(30);
            tcpClientHeartbeatTimeoutShouldActuallyBeUsed = true;

            return this;
        }

        public HalibutTimeoutsAndLimitsBuilder WithTcpKeepAliveEnabled(bool tcpKeepAliveEnabled)
        {
            this.tcpKeepAliveEnabled = tcpKeepAliveEnabled;
            return this;
        }

        public HalibutTimeoutsAndLimitsBuilder WithOverride(
            TimeSpan? pollingRequestQueueTimeout = null,
            TimeSpan? pollingRequestMaximumMessageProcessingTimeout = null,
            TimeSpan? retryListeningSleepInterval = null,
            int? retryCountLimit = null,
            TimeSpan? connectionErrorRetryTimeout = null,
            int? rewindableBufferStreamSize = null,
            SendReceiveTimeout? tcpClientTimeout = null,
            TimeSpan? tcpClientReceiveResponseTimeout = null,
            TimeSpan? tcpClientReceiveRequestTimeoutForPolling = null,
            TimeSpan? tcpClientPooledConnectionTimeout = null,
            TimeSpan? tcpListeningNextRequestIdleTimeout = null,
            SendReceiveTimeout? tcpClientHeartbeatTimeout = null,
            bool? tcpClientHeartbeatTimeoutShouldActuallyBeUsed = null,
            TimeSpan? tcpClientConnectTimeout = null,
            TimeSpan? pollingQueueWaitTimeout = null,
            bool? tcpKeepAliveEnabled = null,
            int? tcpKeepAliveRetryCount = null,
            TimeSpan? tcpKeepAliveTime = null,
            TimeSpan? tcpKeepAliveInterval = null)
        {
            this.pollingRequestQueueTimeout = pollingQueueWaitTimeout ?? this.pollingRequestQueueTimeout;
            this.pollingRequestMaximumMessageProcessingTimeout = pollingRequestMaximumMessageProcessingTimeout ?? this.pollingRequestMaximumMessageProcessingTimeout;
            this.retryListeningSleepInterval = retryListeningSleepInterval ?? this.retryListeningSleepInterval;
            this.retryCountLimit = retryCountLimit ?? this.retryCountLimit;
            this.connectionErrorRetryTimeout = connectionErrorRetryTimeout ?? this.connectionErrorRetryTimeout;
            this.rewindableBufferStreamSize = rewindableBufferStreamSize ?? this.rewindableBufferStreamSize;
            this.tcpClientTimeout = tcpClientTimeout ?? this.tcpClientTimeout;
            this.tcpClientReceiveResponseTimeout = tcpClientReceiveResponseTimeout ?? this.tcpClientReceiveResponseTimeout;
            this.tcpClientReceiveRequestTimeoutForPolling = tcpClientReceiveRequestTimeoutForPolling ?? this.tcpClientReceiveRequestTimeoutForPolling;
            this.tcpClientPooledConnectionTimeout = tcpClientPooledConnectionTimeout ?? this.tcpClientPooledConnectionTimeout;
            this.tcpListeningNextRequestIdleTimeout = tcpListeningNextRequestIdleTimeout ?? this.tcpListeningNextRequestIdleTimeout;
            this.tcpClientHeartbeatTimeout = tcpClientHeartbeatTimeout ?? this.tcpClientHeartbeatTimeout;
            this.tcpClientHeartbeatTimeoutShouldActuallyBeUsed = tcpClientHeartbeatTimeoutShouldActuallyBeUsed ?? this.tcpClientHeartbeatTimeoutShouldActuallyBeUsed;
            this.tcpClientConnectTimeout = tcpClientConnectTimeout ?? this.tcpClientConnectTimeout;
            this.pollingQueueWaitTimeout = pollingQueueWaitTimeout ?? this.pollingQueueWaitTimeout;
            this.tcpKeepAliveEnabled = tcpKeepAliveEnabled ?? this.tcpKeepAliveEnabled;
            this.tcpKeepAliveRetryCount = tcpKeepAliveRetryCount ?? this.tcpKeepAliveRetryCount;
            this.tcpKeepAliveTime = tcpKeepAliveTime ?? this.tcpKeepAliveTime;
            this.tcpKeepAliveInterval = tcpKeepAliveInterval ?? this.tcpKeepAliveInterval;
            return this;
        }

        public HalibutTimeoutsAndLimits Build()
        {
            return new HalibutTimeoutsAndLimits(
                pollingRequestQueueTimeout,
                pollingRequestMaximumMessageProcessingTimeout,
                retryListeningSleepInterval,
                retryCountLimit,
                connectionErrorRetryTimeout,
                rewindableBufferStreamSize,
                tcpClientTimeout,
                tcpClientReceiveResponseTimeout,
                tcpClientReceiveRequestTimeoutForPolling,
                tcpClientPooledConnectionTimeout,
                tcpListeningNextRequestIdleTimeout,
                tcpClientHeartbeatTimeout,
                tcpClientHeartbeatTimeoutShouldActuallyBeUsed,
                tcpClientConnectTimeout,
                pollingQueueWaitTimeout,
                tcpKeepAliveEnabled,
                tcpKeepAliveRetryCount,
                tcpKeepAliveTime,
                tcpKeepAliveInterval);
        }
    }
}