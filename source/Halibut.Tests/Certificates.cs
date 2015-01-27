using System;
using System.Security.Cryptography.X509Certificates;

namespace Halibut.Tests
{
    public class Certificates
    {
        public static X509Certificate2 TentacleListening;
        public static string TentacleListeningPublicThumbprint;
        public static X509Certificate2 Octopus;
        public static string OctopusPublicThumbprint;
        public static X509Certificate2 TentaclePolling;
        public static string TentaclePollingPublicThumbprint;

        static Certificates()
        {
            TentacleListening = new X509Certificate2("Certificates\\TentacleListening.pfx");
            TentacleListeningPublicThumbprint = TentacleListening.Thumbprint;
            Octopus = new X509Certificate2("Certificates\\Octopus.pfx");
            OctopusPublicThumbprint = Octopus.Thumbprint;
            TentaclePolling = new X509Certificate2("Certificates\\TentaclePolling.pfx");
            TentaclePollingPublicThumbprint = TentaclePolling.Thumbprint;
        }
    }
}