using System;
using Newtonsoft.Json;

namespace Halibut
{
    public class ServiceEndPoint : IEquatable<ServiceEndPoint>
    {
        readonly Uri baseUri;
        readonly string remoteThumbprint;

        public ServiceEndPoint(string baseUri, string remoteThumbprint)
            : this(new Uri(baseUri), remoteThumbprint)
        {
        }

        [JsonConstructor]
        public ServiceEndPoint(Uri baseUri, string remoteThumbprint)
        {
            this.baseUri = baseUri;
            this.remoteThumbprint = remoteThumbprint;
        }

        public Uri BaseUri
        {
            get { return baseUri; }
        }

        public string RemoteThumbprint
        {
            get { return remoteThumbprint; }
        }

        public override string ToString()
        {
            return baseUri.ToString();
        }

        public bool Equals(ServiceEndPoint other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(baseUri, other.baseUri) && string.Equals(remoteThumbprint, other.remoteThumbprint);
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
                return ((baseUri != null ? baseUri.GetHashCode() : 0)*397) ^ (remoteThumbprint != null ? remoteThumbprint.GetHashCode() : 0);
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