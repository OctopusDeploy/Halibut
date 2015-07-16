using System;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Halibut
{
    public class ServiceEndPoint : IEquatable<ServiceEndPoint>
    {
        readonly Uri baseUri;
        readonly List<string> remoteThumbprints;
        readonly string baseUriString;

        public ServiceEndPoint(string baseUri, string remoteThumbprint)
            : this(new Uri(baseUri), remoteThumbprint)
        {
        }

        public ServiceEndPoint(Uri baseUri, string remoteThumbprint)
        {
            baseUriString = baseUri.GetLeftPart(UriPartial.Authority).ToLowerInvariant();
            this.baseUri = new Uri(baseUriString);
            List<string> remoteThumbprints = new List<string>();
            remoteThumbprints.Add(remoteThumbprint);
            this.remoteThumbprints = remoteThumbprints;
        }

        public ServiceEndPoint(string baseUri, List<string> remoteThumbprints)
            : this(new Uri(baseUri), remoteThumbprints)
        {
        }

        [JsonConstructor]
        public ServiceEndPoint(Uri baseUri, List<string> remoteThumbprints)
        {
            baseUriString = baseUri.GetLeftPart(UriPartial.Authority).ToLowerInvariant();
            this.baseUri = new Uri(baseUriString);
            this.remoteThumbprints = remoteThumbprints;
        }

        public Uri BaseUri
        {
            get { return baseUri; }
        }

        public List<string> RemoteThumbprints
        {
            get { return remoteThumbprints; }
        }

        public override string ToString()
        {
            return baseUriString;
        }

        public bool Equals(ServiceEndPoint other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(baseUriString, other.baseUriString) && string.Equals(remoteThumbprints, other.remoteThumbprints);
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
                return ((baseUriString != null ? baseUriString.GetHashCode() : 0) * 397) ^ (remoteThumbprints != null ? remoteThumbprints.GetHashCode() : 0);
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