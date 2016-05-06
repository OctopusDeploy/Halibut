using System;
using Newtonsoft.Json;

namespace Halibut
{
    public class ServiceEndPoint : IEquatable<ServiceEndPoint>
    {
        readonly Uri baseUri;
        readonly string remoteThumbprint;
        readonly string baseUriString;
        readonly ProxyDetails proxy;

        public ServiceEndPoint(string baseUri, string remoteThumbprint)
            : this(new Uri(baseUri), remoteThumbprint)
        {
        }

        public ServiceEndPoint(Uri baseUri, string remoteThumbprint)
        {
            baseUriString = baseUri.GetLeftPart(UriPartial.Authority).ToLowerInvariant();
            this.baseUri = new Uri(baseUriString);
            this.remoteThumbprint = remoteThumbprint;
        }

        [JsonConstructor]
        public ServiceEndPoint(Uri baseUri, string remoteThumbprint, ProxyDetails proxy)
        {
            baseUriString = baseUri.GetLeftPart(UriPartial.Authority).ToLowerInvariant();
            this.baseUri = new Uri(baseUriString);
            this.remoteThumbprint = remoteThumbprint;
            this.proxy = proxy;
        }

        public Uri BaseUri
        {
            get { return baseUri; }
        }

        public string RemoteThumbprint
        {
            get { return remoteThumbprint; }
        }

        public ProxyDetails Proxy
        {
            get { return proxy; }
        }

        public override string ToString()
        {
            return baseUriString;
        }

        public bool Equals(ServiceEndPoint other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(remoteThumbprint, other.remoteThumbprint) && string.Equals(baseUriString, other.baseUriString) && Equals(proxy, other.proxy);
        }

        public override bool Equals(object obj)
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
                var hashCode = (remoteThumbprint != null ? remoteThumbprint.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (baseUriString != null ? baseUriString.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (proxy != null ? proxy.GetHashCode() : 0);
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