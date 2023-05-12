using System;
using System.Security.Cryptography.X509Certificates;

namespace Halibut.Tests.Util
{
    public class CertAndThumbprint
    {
        public static CertAndThumbprint TentacleListening = new(Certificates.TentacleListeningPfxPath, Certificates.TentacleListening, Certificates.TentacleListeningPublicThumbprint);
        public static CertAndThumbprint TentaclePolling = new(Certificates.TentaclePollingPfxPath, Certificates.TentaclePolling, Certificates.TentaclePollingPublicThumbprint);
        public static CertAndThumbprint Octopus = new(Certificates.OctopusPfxPath, Certificates.Octopus, Certificates.OctopusPublicThumbprint);

        public CertAndThumbprint(string filePath, X509Certificate2 certificate2, string thumbprint)
        {
            Certificate2 = certificate2;
            Thumbprint = thumbprint;
            FilePath = filePath;
        }

        public X509Certificate2 Certificate2 { get; }
        public string FilePath { get; }
        public string Thumbprint { get; }
    }
}