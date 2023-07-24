using System;
using System.Security.Cryptography.X509Certificates;

namespace Halibut.Tests.Support
{
    public class CertAndThumbprint
    {
        public static CertAndThumbprint TentacleListening = new(Certificates.TentacleListeningPfxPath, Certificates.TentacleListening, Certificates.TentacleListeningPublicThumbprint);
        public static CertAndThumbprint TentaclePolling = new(Certificates.TentaclePollingPfxPath, Certificates.TentaclePolling, Certificates.TentaclePollingPublicThumbprint);
        public static CertAndThumbprint Octopus = new(Certificates.OctopusPfxPath, Certificates.Octopus, Certificates.OctopusPublicThumbprint);
        public static CertAndThumbprint Wrong = new(Certificates.WrongPfxPath, Certificates.Wrong, Certificates.WrongPublicThumbprint);

        public CertAndThumbprint(string certificatePfxPath, X509Certificate2 certificate2, string thumbprint)
        {
            Certificate2 = certificate2;
            Thumbprint = thumbprint;
            CertificatePfxPath = certificatePfxPath;
        }

        public X509Certificate2 Certificate2 { get; }
        public string CertificatePfxPath { get; }
        public string Thumbprint { get; }
    }
}