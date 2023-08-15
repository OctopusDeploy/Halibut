using System;
using System.Reflection;

namespace Halibut.Diagnostics
{
    [Obsolete]
    public class HalibutLimits
    {
        static HalibutLimits()
        {
            var settings = System.Configuration.ConfigurationManager.AppSettings;

            var fields = typeof (HalibutLimits).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
            foreach (var field in fields)
            {
                var value = settings.Get("Halibut." + field.Name);
                if (string.IsNullOrWhiteSpace(value)) continue;
                var time = TimeSpan.Parse(value);
                field.SetValue(null, time);
            }
        }

        /// <summary>
        /// The default amount of time the client will wait for the server to collect a message from the
        /// polling request queue before raising a TimeoutException. Can be overridden via the ServiceEndPoint.
        /// </summary>
        public static TimeSpan PollingRequestQueueTimeout = TimeSpan.FromMinutes(2);

        /// <summary>
        /// The default amount of time the client will wait for the server to process a message collected
        /// from the polling request queue before it raises a TimeoutException. Can be overridden via the ServiceEndPoint.
        /// </summary>
        public static TimeSpan PollingRequestMaximumMessageProcessingTimeout = TimeSpan.FromMinutes(10);
        
        /// <summary>
        /// The amount of time to wait between connection requests to the remote endpoint (applies
        /// to both polling and listening connections). Can be overridden via the ServiceEndPoint.
        /// </summary>
        public static TimeSpan RetryListeningSleepInterval = TimeSpan.FromSeconds(1);
        
        /// <summary>
        /// The number of times to try and connect to the remote endpoint. Can be overridden via the ServiceEndPoint.
        /// </summary>
        public static int RetryCountLimit = 5;
        
        /// <summary>
        /// Stops connection retries if this time period has been exceeded from the initial connection attempt. Can be overridden via the ServiceEndPoint.
        /// </summary>
        public static TimeSpan ConnectionErrorRetryTimeout = TimeSpan.FromMinutes(5);

        /// <summary>
        /// The size of the buffer, in bytes, of the rewind buffer when reading compressed message envelopes.
        /// </summary>
        /// <remarks>
        /// For safety, this should match the buffer size of the decorated stream (i.e. DeflateStream) to avoid unintended side-effects.
        /// </remarks>
        public static int RewindableBufferStreamSize = 8192;
        
        /// <summary>
        /// Amount of time to wait for a TCP or SslStream write to complete successfully
        /// </summary>
        public static TimeSpan TcpClientSendTimeout = TimeSpan.FromMinutes(10);
        
        /// <summary>
        /// Amount of time to wait for a TCP or SslStream read to complete successfully
        /// </summary>
        public static TimeSpan TcpClientReceiveTimeout = TimeSpan.FromMinutes(10);
        
        /// <summary>
        /// Amount of time a connection can stay in the pool
        /// </summary>
        public static TimeSpan TcpClientPooledConnectionTimeout = TimeSpan.FromMinutes(9);
        
        public static TimeSpan TcpClientHeartbeatSendTimeout = TimeSpan.FromSeconds(60);
        public static TimeSpan TcpClientHeartbeatReceiveTimeout = TimeSpan.FromSeconds(60);
        
        /// <summary>
        /// Amount of time to wait for a successful TCP or WSS connection
        /// </summary>
        public static TimeSpan TcpClientConnectTimeout = TimeSpan.FromSeconds(60);
        
        /// <summary>
        /// The amount of time client will wait for a message to be added to the polling request queue
        /// before returning a null response to the server. This does not generate an error and the server would immediate re-request.
        /// </summary>
        public static TimeSpan PollingQueueWaitTimeout = TimeSpan.FromSeconds(30);

        // After a client/server message exchange is complete, the client returns
        // the connection to the pool but the server continues to block and reads
        // from the connection until the TcpClientReceiveTimeout.
        // If TcpClientPooledConnectionTimeout is greater than TcpClientReceiveTimeout
        // when the client goes to the pool to get a connection for the next
        // exchange it can get one that has timed out, so make sure our pool
        // timeout is smaller than the tcp timeout.
        public static TimeSpan SafeTcpClientPooledConnectionTimeout
        {
            get
            {
                if (TcpClientPooledConnectionTimeout < TcpClientReceiveTimeout)
                {
                    return TcpClientPooledConnectionTimeout;
                }
                else
                {
                    var timeout = TcpClientReceiveTimeout - TimeSpan.FromSeconds(10);
                    return timeout > TimeSpan.Zero ? timeout : TcpClientReceiveTimeout;
                }
            }
        }
    }
#pragma warning disable CS0612
    public class HalibutTimeoutsAndLimits
    {
        /// <summary>
        ///     The default amount of time the client will wait for the server to collect a message from the
        ///     polling request queue before raising a TimeoutException. Can be overridden via the ServiceEndPoint.
        /// </summary>
        public TimeSpan PollingRequestQueueTimeout { get; set; } = HalibutLimits.PollingRequestQueueTimeout;

        /// <summary>
        ///     The default amount of time the client will wait for the server to process a message collected
        ///     from the polling request queue before it raises a TimeoutException. Can be overridden via the ServiceEndPoint.
        /// </summary>
        public TimeSpan PollingRequestMaximumMessageProcessingTimeout { get; set; } = HalibutLimits.PollingRequestMaximumMessageProcessingTimeout;

        /// <summary>
        ///     The amount of time to wait between connection requests to the remote endpoint (applies
        ///     to both polling and listening connections). Can be overridden via the ServiceEndPoint.
        /// </summary>
        public TimeSpan RetryListeningSleepInterval { get; set; } = HalibutLimits.RetryListeningSleepInterval;

        /// <summary>
        ///     The number of times to try and connect to the remote endpoint. Can be overridden via the ServiceEndPoint.
        /// </summary>
        public int RetryCountLimit { get; set; } = HalibutLimits.RetryCountLimit;

        /// <summary>
        ///     Stops connection retries if this time period has been exceeded from the initial connection attempt. Can be
        ///     overridden via the ServiceEndPoint.
        /// </summary>
        public TimeSpan ConnectionErrorRetryTimeout { get; set; } = HalibutLimits.ConnectionErrorRetryTimeout;

        /// <summary>
        ///     The size of the buffer, in bytes, of the rewind buffer when reading compressed message envelopes.
        /// </summary>
        /// <remarks>
        ///     For safety, this should match the buffer size of the decorated stream (i.e. DeflateStream) to avoid unintended
        ///     side-effects.
        /// </remarks>
        public int RewindableBufferStreamSize { get; set; } = HalibutLimits.RewindableBufferStreamSize;

        /// <summary>
        ///     Amount of time to wait for a TCP or SslStream write to complete successfully
        /// </summary>
        public TimeSpan TcpClientSendTimeout { get; set; } = HalibutLimits.TcpClientSendTimeout;

        /// <summary>
        ///     Amount of time to wait for a TCP or SslStream read to complete successfully
        /// </summary>
        public TimeSpan TcpClientReceiveTimeout { get; set; } = HalibutLimits.TcpClientReceiveTimeout;

        /// <summary>
        ///     Amount of time a connection can stay in the pool
        /// </summary>
        public TimeSpan TcpClientPooledConnectionTimeout { get; set; } = HalibutLimits.TcpClientPooledConnectionTimeout;

        public TimeSpan TcpClientHeartbeatSendTimeout { get; set; } = HalibutLimits.TcpClientHeartbeatSendTimeout;
        public TimeSpan TcpClientHeartbeatReceiveTimeout { get; set; } = HalibutLimits.TcpClientHeartbeatReceiveTimeout;

        /// <summary>
        ///     Amount of time to wait for a successful TCP or WSS connection
        /// </summary>
        public TimeSpan TcpClientConnectTimeout { get; set; } = HalibutLimits.TcpClientConnectTimeout;

        /// <summary>
        ///     The amount of time client will wait for a message to be added to the polling request queue
        ///     before returning a null response to the server. This does not generate an error and the server would immediate
        ///     re-request.
        /// </summary>
        public TimeSpan PollingQueueWaitTimeout { get; set; } = HalibutLimits.PollingQueueWaitTimeout;
#pragma warning restore CS0612
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
                if (TcpClientPooledConnectionTimeout < TcpClientReceiveTimeout)
                {
                    return TcpClientPooledConnectionTimeout;
                }

                var timeout = TcpClientReceiveTimeout - TimeSpan.FromSeconds(10);
                return timeout > TimeSpan.Zero ? timeout : TcpClientReceiveTimeout;
            }
        }
    }
}