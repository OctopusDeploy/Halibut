using System;
using System.Security.Cryptography.X509Certificates;

namespace Halibut.Tests.Util
{
    public class CertAndThumbprint
    {
        public static CertAndThumbprint TentacleListening = new(Certificates.TentacleListening, Certificates.TentacleListeningPublicThumbprint);
        public static CertAndThumbprint TentaclePolling = new(Certificates.TentaclePolling, Certificates.TentaclePollingPublicThumbprint);
        public static CertAndThumbprint Octopus = new(Certificates.Octopus, Certificates.OctopusPublicThumbprint);

        public CertAndThumbprint(X509Certificate2 certificate2, string thumbprint)
        {
            Certificate2 = certificate2;
            Thumbprint = thumbprint;
        }

        public X509Certificate2 Certificate2 { get; }
        public string Thumbprint { get; }
    }
}