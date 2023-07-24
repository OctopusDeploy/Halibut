using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;

namespace Halibut.Tests.Support
{
    public class Certificates
    {
        public static X509Certificate2 TentacleListening;
        public static string TentacleListeningPublicThumbprint;
        public static string TentacleListeningPfxPath;

        public static X509Certificate2 Octopus;
        public static string OctopusPublicThumbprint;
        public static string OctopusPfxPath;

        public static X509Certificate2 TentaclePolling;
        public static string TentaclePollingPublicThumbprint;
        public static string TentaclePollingPfxPath;
        
        public static X509Certificate2 Wrong;
        public static string WrongPublicThumbprint;
        public static string WrongPfxPath;


        public static X509Certificate2 Ssl;
        public static string SslThumbprint;

        static Certificates()
        {
            //jump through hoops to find certs because the nunit test runner is messing with directories
            var directory = Path.Combine(Path.GetDirectoryName(new Uri(typeof(Certificates).Assembly.CodeBase).LocalPath), "Certificates");
            TentacleListeningPfxPath = Path.Combine(directory, "TentacleListening.pfx");
            TentacleListening = new X509Certificate2(TentacleListeningPfxPath);
            TentacleListeningPublicThumbprint = TentacleListening.Thumbprint;

            OctopusPfxPath = Path.Combine(directory, "Octopus.pfx");
            Octopus = new X509Certificate2(OctopusPfxPath);
            OctopusPublicThumbprint = Octopus.Thumbprint;

            TentaclePollingPfxPath = Path.Combine(directory, "TentaclePolling.pfx");
            TentaclePolling = new X509Certificate2(TentaclePollingPfxPath);
            TentaclePollingPublicThumbprint = TentaclePolling.Thumbprint;
            
            WrongPfxPath = Path.Combine(directory, "WrongCert.pfx");
            Wrong = new X509Certificate2(WrongPfxPath);
            WrongPublicThumbprint = Wrong.Thumbprint;

            Ssl = new X509Certificate2(Path.Combine(directory, "Ssl.pfx"), "password");
            SslThumbprint = Ssl.Thumbprint;
        }
    }
}