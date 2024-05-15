using System;
using Octopus.TestPortForwarder;

namespace Halibut.Diagnostics
{
    public class HalibutTimeoutsAndLimits
    {
        public HalibutTimeoutsAndLimits() { }

        /// <summary>
        ///     The default amount of time the client will wait for the server to collect a message from the
        ///     polling request queue before raising a TimeoutException. Can be overridden via the ServiceEndPoint.
        /// </summary>
        public TimeSpan PollingRequestQueueTimeout { get; set; } = TimeSpan.FromMinutes(2);

        /// <summary>
        ///     The amount of time to wait between connection requests to the remote endpoint (applies
        ///     to both polling and listening connections). Can be overridden via the ServiceEndPoint.
        /// </summary>
        public TimeSpan RetryListeningSleepInterval { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        ///     The number of times to try and connect to the remote endpoint. Can be overridden via the ServiceEndPoint.
        /// </summary>
        public int RetryCountLimit { get; set; } = 5;

        /// <summary>
        ///     Stops connection retries if this time period has been exceeded from the initial connection attempt. Can be
        ///     overridden via the ServiceEndPoint.
        /// </summary>
        public TimeSpan ConnectionErrorRetryTimeout { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        ///     The size of the buffer, in bytes, of the rewind buffer when reading compressed message envelopes.
        /// </summary>
        /// <remarks>
        ///     For safety, this should match the buffer size of the decorated stream (i.e. DeflateStream) to avoid unintended
        ///     side-effects.
        /// </remarks>
        public int RewindableBufferStreamSize { get; set; } = 8192;

        /// <summary>
        /// Amount of time to wait for a TCP read/write to complete successfully.
        ///
        /// This Timeout is used when no other more specific timeout applies.
        ///
        /// This applies to:
        /// - Initial authentication and identification
        /// - Sending and receiving of Request/Response messages, except for the first byte in some instances.
        /// - Sending/receiving data streams.
        /// </summary>
        public SendReceiveTimeout TcpClientTimeout { get; set; } = new(sendTimeout: TimeSpan.FromMinutes(10), receiveTimeout: TimeSpan.FromMinutes(10));

        /// <summary>
        ///    Approximately the amount of time the service can take to execute the RPC call.
        ///
        ///    Specifically the amount of time to wait for the first byte of the response to arrive.
        /// </summary>
        public TimeSpan TcpClientReceiveResponseTimeout { get; set; } = TimeSpan.FromMinutes(10);

        /// <summary>
        ///     Amount of time the polling service will wait for a request from an RPC call.
        ///     Specifically the amount of time to wait for the first byte of the request to arrive.
        ///
        ///     This value must never be less than the PollingQueueWaitTimeout, of the client, since this
        ///     is the timeout of the long poll the polling service makes to the client to get the next request.
        ///
        ///     Currently set to 10 minutes as that is what the timeout used to be.
        /// </summary>
        public TimeSpan TcpClientReceiveRequestTimeoutForPolling { get; set; } = TimeSpan.FromMinutes(10);

        /// <summary>
        ///     Amount of time a connection can stay in the pool
        /// </summary>
        public TimeSpan TcpClientPooledConnectionTimeout { get; set; } = TimeSpan.FromMinutes(9);

        /// <summary>
        /// The duration that the listening service will wait for the next request (specifically the
        /// control message NEXT) before closing the connection.
        ///
        /// The default is ten minutes, and so the service on an existing TCP connection will wait ten
        /// minutes for another request before "idling out" and closing the connection.
        ///
        /// Connections can be kept idle in the pool (client side) for no more than this timeout, thus
        /// TcpClientPooledConnectionTimeout ought to be less than this.
        /// </summary>
        public TimeSpan TcpListeningNextRequestIdleTimeout { get; set; } = TimeSpan.FromMinutes(10);

        /// <summary>
        ///     Amount of time to wait for a TCP or SslStream read/write to complete successfully for a control message
        ///     This applies only to NEXT/PROCEED/END control messages, and not for the NEXT control message for a
        ///     listening service.
        /// </summary>
        public SendReceiveTimeout TcpClientHeartbeatTimeout { get; set; } = new(sendTimeout: TimeSpan.FromSeconds(60), receiveTimeout: TimeSpan.FromSeconds(60));

        /// <summary>
        ///     When true the TcpClientHeartbeatTimeout is used in all places it can be.
        ///     Will be removed in the future.
        /// </summary>
        public bool TcpClientHeartbeatTimeoutShouldActuallyBeUsed { get; set; } = false;

        /// <summary>
        ///     Amount of time to wait for a successful TCP or WSS connection
        /// </summary>
        public TimeSpan TcpClientConnectTimeout { get; set; } = TimeSpan.FromSeconds(60);

        /// <summary>
        ///     The amount of time client will wait for a message to be added to the polling request queue
        ///     before returning a null response to the server. This does not generate an error and the server would immediate
        ///     re-request.
        /// </summary>
        public TimeSpan PollingQueueWaitTimeout { get; set; } = TimeSpan.FromSeconds(30);

        // After a client/server message exchange is complete, the client returns
        // the connection to the pool but the service continues to block and reads
        // from the connection until the TcpListeningNextRequestIdleTimeout.
        // If TcpClientPooledConnectionTimeout is greater than TcpClientReceiveTimeout
        // when the client goes to the pool to get a connection for the next
        // exchange it can get one that has timed out, so make sure our pool
        // timeout is smaller than the tcp timeout.
        // Note that if a timed out connection is picked up, the control messages initially
        // sent in the exchange will detect the dead connection resulting in a new connection
        // created for the request.
        public TimeSpan SafeTcpClientPooledConnectionTimeout
        {
            get
            {
                if (TcpClientPooledConnectionTimeout < TcpListeningNextRequestIdleTimeout)
                {
                    return TcpClientPooledConnectionTimeout;
                }

                var timeout = TcpListeningNextRequestIdleTimeout - TimeSpan.FromSeconds(10);
                return timeout > TimeSpan.Zero ? timeout : TcpListeningNextRequestIdleTimeout;
            }
        }

        /// <summary>
        /// Whether we want to use TCP keep alive or not.
        /// </summary>
        public bool TcpKeepAliveEnabled { get; set; } = true;

        /// <summary>
        /// The number of TCP keep alive probes that will be sent before the connection is terminated.
        /// </summary>
        public int TcpKeepAliveRetryCount { get; set; } = 10;

        /// <summary>
        /// The duration a TCP connection will remain alive/idle before keepalive probes are sent to the remote.
        /// </summary>
        public TimeSpan TcpKeepAliveTime { get; set; } = TimeSpan.FromSeconds(15);

        /// <summary>
        /// The duration a TCP connection will wait for a keepalive response before sending another keepalive probe.
        /// </summary>
        public TimeSpan TcpKeepAliveInterval { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// The maximum number of active TCP connections per polling subscription. <c>null</c> indicates there is no limit.
        /// </summary>
        /// <remarks>
        /// This setting is used to prevent denial-of-service/connection exhaustion due to too many incoming connections from a single polling subscription.
        /// The number of authorized, active connections are aggregated per polling subscription, and new connections that exceed the limit are rejected.
        /// </remarks>
        public int? MaximumActiveTcpConnectionsPerPollingSubscription { get; set; }
        
        public bool UseAsyncListener { get; set; }
        
        /// <summary>
        /// Sets https://learn.microsoft.com/en-us/dotnet/api/system.net.sockets.tcpclient.nodelay?view=net-8.0
        /// Previous value was false, we will be moving to true
        /// </summary>
        public bool TcpNoDelay { get; set; }

        /// <summary>
        /// In the future these will become the default
        /// </summary>
        /// <returns></returns>
        public static HalibutTimeoutsAndLimits RecommendedValues()
        {
            return new HalibutTimeoutsAndLimits()
            {
                // In general all writes/read calls should take less than a minute.
                TcpClientTimeout = new(sendTimeout: TimeSpan.FromMinutes(1), receiveTimeout: TimeSpan.FromMinutes(1)),
                TcpClientReceiveResponseTimeout = TimeSpan.FromMinutes(5), // ~ 5 minutes to execute a RPC call
                TcpClientReceiveRequestTimeoutForPolling = new HalibutTimeoutsAndLimits().PollingQueueWaitTimeout + TimeSpan.FromSeconds(30),
                TcpClientHeartbeatTimeoutShouldActuallyBeUsed = true
            };
        }
    }
}