using System;
using Newtonsoft.Json;

namespace Halibut
{
    public class ServiceEndPoint : IEquatable<ServiceEndPoint>
    {
        readonly string baseUriString;

        public ServiceEndPoint(string baseUri, string remoteThumbprint)
            : this(new Uri(baseUri), remoteThumbprint)
        {
        }

        public ServiceEndPoint(Uri baseUri, string remoteThumbprint)
            : this(baseUri, remoteThumbprint, null)
        {
        }

        [JsonConstructor]
        public ServiceEndPoint(Uri baseUri, string remoteThumbprint, ProxyDetails proxy)
        {
            if (baseUri.Scheme == "wss")
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
        }

        public Uri BaseUri { get; }

        public string RemoteThumbprint { get; }

        public ProxyDetails Proxy { get; }

        public override string ToString() => baseUriString;

        public bool Equals(ServiceEndPoint other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(RemoteThumbprint, other.RemoteThumbprint) && string.Equals(baseUriString, other.baseUriString) && Equals(Proxy, other.Proxy);
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
                var hashCode = (RemoteThumbprint != null ? RemoteThumbprint.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (baseUriString != null ? baseUriString.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (Proxy != null ? Proxy.GetHashCode() : 0);
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