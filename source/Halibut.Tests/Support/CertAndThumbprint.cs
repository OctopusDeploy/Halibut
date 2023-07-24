using System;
using System.Security.Cryptography.X509Certificates;

namespace Halibut.Tests.Support
{
    public class CertAndThumbprint
    {
        /// <summary>
        /// CN=Halibut Alice
        /// Thumbprint: 36F35047CE8B000CF4C671819A2DD1AFCDE3403D
        /// </summary>
        public static CertAndThumbprint TentacleListening = new(Certificates.TentacleListeningPfxPath, Certificates.TentacleListening);
        
        /// <summary>
        /// CN=Halibut Eve
        /// Thumbprint: 4098EC3A2FC2B92B97339D3831BA230CC1DD590F
        /// </summary>
        public static CertAndThumbprint TentaclePolling = new(Certificates.TentaclePollingPfxPath, Certificates.TentaclePolling);
        
        /// <summary>
        /// CN=Halibut Bob
        /// Thumbprint: 76225C0717A16C1D0BA4A7FFA76519D286D8A248
        /// </summary>
        public static CertAndThumbprint Octopus = new(Certificates.OctopusPfxPath, Certificates.Octopus);
        
        /// <summary>
        /// 
        /// CN=expected
        /// Thumbprint: EC32122053C6BFF582F8246F5697633D06F0F97F
        /// </summary>
        public static CertAndThumbprint Wrong = new(Certificates.WrongPfxPath, Certificates.Wrong);
        
        /// <summary>
        /// E=Halibut, CN=Halibut Sample
        /// Thumbprint: 6E5C6492129B75A4C83E1A23797AF6344092E5C2
        /// </summary>
        public static CertAndThumbprint Ssl = new(Certificates.sslPfxPath, Certificates.Ssl);

        public CertAndThumbprint(string certificatePfxPath, X509Certificate2 certificate2)
        {
            Certificate2 = certificate2;
            CertificatePfxPath = certificatePfxPath;
        }

        public X509Certificate2 Certificate2 { get; }
        public string CertificatePfxPath { get; }
        public string Thumbprint => Certificate2.Thumbprint;
    }
}