using System;
using Halibut.Diagnostics;
using Newtonsoft.Json;

namespace Halibut
{
    public class ServiceEndPoint : IEquatable<ServiceEndPoint>
    {
        readonly string baseUriString;

        public ServiceEndPoint(string baseUri, string? remoteThumbprint, HalibutTimeoutsAndLimits halibutTimeoutsAndLimits)
            : this(new Uri(baseUri), remoteThumbprint, null, halibutTimeoutsAndLimits)
        {
        }

        public ServiceEndPoint(Uri baseUri, string? remoteThumbprint, HalibutTimeoutsAndLimits halibutTimeoutsAndLimits)
            : this(baseUri, remoteThumbprint, null, halibutTimeoutsAndLimits)
        {
        }

        [JsonConstructor]
        public ServiceEndPoint(Uri baseUri, string? remoteThumbprint, ProxyDetails? proxy, HalibutTimeoutsAndLimits halibutTimeoutsAndLimits)
        {
            if (IsWebSocketAddress(baseUri))
            {
                baseUriString = baseUri.AbsoluteUri;
                BaseUri = baseUri;
            }
            else
            {
                baseUriString = baseUri.GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped).ToLowerInvariant();
                BaseUri = new Uri(baseUriString);
            }
            RemoteThumbprint = remoteThumbprint;
            Proxy = proxy;

            halibutTimeoutsAndLimits ??= new HalibutTimeoutsAndLimits();

            this.PollingRequestQueueTimeout = halibutTimeoutsAndLimits.PollingRequestQueueTimeout;
            this.PollingRequestMaximumMessageProcessingTimeout = halibutTimeoutsAndLimits.PollingRequestMaximumMessageProcessingTimeout;
            this.RetryListeningSleepInterval = halibutTimeoutsAndLimits.RetryListeningSleepInterval;
            this.RetryCountLimit = halibutTimeoutsAndLimits.RetryCountLimit;
            this.ConnectionErrorRetryTimeout = halibutTimeoutsAndLimits.ConnectionErrorRetryTimeout;
            this.TcpClientConnectTimeout = halibutTimeoutsAndLimits.TcpClientConnectTimeout;
        }

        /// <summary>
        /// The amount of time the client will wait for the server to collect a message from the
        /// polling request queue before raising a TimeoutException
        /// </summary>
        public TimeSpan PollingRequestQueueTimeout { get; set; }


        /// <summary>
        /// The amount of time the client will wait for the server to process a message collected
        /// from the polling request queue before it raises a TimeoutException
        /// </summary>
        public TimeSpan PollingRequestMaximumMessageProcessingTimeout { get; set; }

        /// <summary>
        /// The amount of time to wait between connection requests to the remote endpoint (applies
        /// to both polling and listening connections)
        /// </summary>
        public TimeSpan RetryListeningSleepInterval { get; set; }

        /// <summary>
        /// The number of times to try and connect to the remote endpoint
        /// </summary>
        public int RetryCountLimit { get; set; }

        /// <summary>
        /// Stops connection retries if this time period has been exceeded from the initial connection attempt
        /// </summary>
        public TimeSpan ConnectionErrorRetryTimeout { get; set; }

        /// <summary>
        /// Amount of time to wait for a successful TCP or WSS connection
        /// </summary>
        public TimeSpan TcpClientConnectTimeout { get; set; }

        public Uri BaseUri { get; }

        public string? RemoteThumbprint { get; }

        public ProxyDetails? Proxy { get; }

        public bool IsWebSocketEndpoint => IsWebSocketAddress(BaseUri);

        public override string ToString() => baseUriString;

        public static bool IsWebSocketAddress(Uri baseUri) => baseUri.Scheme.Equals("wss", StringComparison.OrdinalIgnoreCase);

        public bool Equals(ServiceEndPoint? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(RemoteThumbprint, other.RemoteThumbprint) && string.Equals(baseUriString, other.baseUriString) && Equals(Proxy, other.Proxy);
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ServiceEndPoint) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (RemoteThumbprint != null ? RemoteThumbprint.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (baseUriString != null ? baseUriString.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (Proxy is not null ? Proxy.GetHashCode() : 0);
                return hashCode;
            }
        }

        public static bool operator ==(ServiceEndPoint left, ServiceEndPoint right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(ServiceEndPoint left, ServiceEndPoint right)
        {
            return !Equals(left, right);
        }
    }
}