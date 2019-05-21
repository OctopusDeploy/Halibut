using System;
using System.Collections.Generic;
using System.Linq;

namespace Halibut.ServiceModel
{
    internal class DefaultTrustProvider : ITrustProvider
    {
        readonly HashSet<string> trustedThumbprints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public void Add(string clientThumbprint)
        {
            lock (trustedThumbprints)
                trustedThumbprints.Add(clientThumbprint);
        }

        public bool IsTrusted(string clientThumbprint)
        {
            lock (trustedThumbprints)
                return trustedThumbprints.Contains(clientThumbprint);
        }

        public void Remove(string clientThumbprint)
        {
            lock (trustedThumbprints)
                trustedThumbprints.Remove(clientThumbprint);
        }

        public void TrustOnly(IReadOnlyList<string> thumbprints)
        {
            lock (trustedThumbprints)
            {
                trustedThumbprints.Clear();
                foreach (var thumbprint in thumbprints)
                    trustedThumbprints.Add(thumbprint);
            }
        }
        public string[] ToArray()
        {
            lock (trustedThumbprints)
                return trustedThumbprints.ToArray();
        }
    }
}
