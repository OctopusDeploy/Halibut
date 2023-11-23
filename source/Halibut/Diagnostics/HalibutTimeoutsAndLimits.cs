using System;

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
        ///     The default amount of time the client will wait for the server to process a message collected
        ///     from the polling request queue before it raises a TimeoutException. Can be overridden via the ServiceEndPoint.
        /// </summary>
        public TimeSpan PollingRequestMaximumMessageProcessingTimeout { get; set; } = TimeSpan.FromMinutes(10);

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
        ///     Amount of time to wait for a TCP or SslStream read/write to complete successfully
        /// </summary>
        public SendReceiveTimeout TcpClientTimeout { get; set; } = new(sendTimeout: TimeSpan.FromMinutes(10), receiveTimeout: TimeSpan.FromMinutes(10));

        /// <summary>
        ///     Amount of time to wait for a response from an RPC call.
        /// 
        ///     Specifically the amount of time to wait for the first byte of the request to arrive.
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
        ///     Amount of time to wait when receiving a response from an RPC call, after data has started being received.
        /// </summary>
        public TimeSpan TcpClientReceiveResponseTransmissionAfterInitialReadTimeout { get; set; } = TimeSpan.FromMinutes(10);
        
        
        /// <summary>
        ///     Amount of time to wait when receiving a Request of an RPC call.
        ///
        ///     For polling services this applies after the TcpClientReceiveRequestTimeoutForPolling has been applied.
        ///
        ///     Currently set to 10 minutes as that is what the timeout used to be. A lower timeout closer to
        ///     the TcpClientHeartbeatTimeout is recommended since this timeout applies when we know we have
        ///     just recently communicated with the client.  
        /// </summary>
        public TimeSpan TcpClientReceiveRequestTransmissionTimeout { get; set; } = TimeSpan.FromMinutes(10);

        /// <summary>
        ///     Amount of time a connection can stay in the pool
        /// </summary>
        public TimeSpan TcpClientPooledConnectionTimeout { get; set; } = TimeSpan.FromMinutes(9);
        
        /// <summary>
        ///     Amount of time to wait for a TCP or SslStream read/write to complete successfully for a control message
        /// </summary>
        public SendReceiveTimeout TcpClientHeartbeatTimeout { get; set; } = new(sendTimeout: TimeSpan.FromSeconds(60), receiveTimeout: TimeSpan.FromSeconds(60));

        /// <summary>
        ///    Timeout for read/writes during the the authentication and identification phase of communication.
        ///
        ///    Currently set to 10 minutes as this was the previous value, a value similar to TcpClientHeartbeatTimeout is recommended.
        /// </summary>
        public SendReceiveTimeout TcpClientAuthenticationAndIdentificationTimeouts { get; set; } = new(sendTimeout: TimeSpan.FromMinutes(10), receiveTimeout: TimeSpan.FromMinutes(10));
        
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
        // the connection to the pool but the server continues to block and reads
        // from the connection until the TcpClientReceiveTimeout.
        // If TcpClientPooledConnectionTimeout is greater than TcpClientReceiveTimeout
        // when the client goes to the pool to get a connection for the next
        // exchange it can get one that has timed out, so make sure our pool
        // timeout is smaller than the tcp timeout.
        public TimeSpan SafeTcpClientPooledConnectionTimeout
        {
            get
            {
                if (TcpClientPooledConnectionTimeout < TcpClientTimeout.ReceiveTimeout)
                {
                    return TcpClientPooledConnectionTimeout;
                }

                var timeout = TcpClientTimeout.ReceiveTimeout - TimeSpan.FromSeconds(10);
                return timeout > TimeSpan.Zero ? timeout : TcpClientTimeout.ReceiveTimeout;
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
    }
}