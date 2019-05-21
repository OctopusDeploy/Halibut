using System;
using System.Collections.Generic;
using System.Text;

namespace Halibut.ServiceModel
{
    public interface ITrustProvider
    {
        void Add(string clientThumbprint);

        //replaces any existing
        void TrustOnly(IReadOnlyList<string> thumbprints);

        void Remove(string clientThumbprint);

        bool IsTrusted(string clientThumbprint);

        string[] ToArray();
    }
}
