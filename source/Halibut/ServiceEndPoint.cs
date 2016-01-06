using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Halibut
{
    public class ServiceEndPoint : IEquatable<ServiceEndPoint>
    {
        readonly Uri baseUri;
        readonly List<string> remoteThumbprints = new List<string>();
        readonly string baseUriString;

        public ServiceEndPoint(string baseUri, string remoteThumbprint, params string[] additionalThumprints)
            : this(new Uri(baseUri), remoteThumbprint, additionalThumprints)
        {
        }

        [JsonConstructor]
        public ServiceEndPoint(Uri baseUri, string remoteThumbprint, params string[] additionalThumbprints)
        {
            baseUriString = baseUri.GetLeftPart(UriPartial.Authority).ToLowerInvariant();
            this.baseUri = new Uri(baseUriString);
            this.remoteThumbprints = new List<string>();
            this.remoteThumbprints.Add(remoteThumbprint);

            if (additionalThumbprints != null)
                this.remoteThumbprints.AddRange(additionalThumbprints);
        }

        public Uri BaseUri
        {
            get { return baseUri; }
        }

        public IList<string> RemoteThumbprints
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
            return Equals(baseUriString, other.baseUriString);
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
                return baseUriString != null ? baseUriString.GetHashCode() : 0;
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