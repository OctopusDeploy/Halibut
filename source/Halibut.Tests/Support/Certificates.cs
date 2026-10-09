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
        public static string sslPfxPath;

        static Certificates()
        {
            //jump through hoops to find certs because the nunit test runner is messing with directories
            var directory = Path.Combine(Path.GetDirectoryName(new Uri(typeof(Certificates).Assembly.Location).LocalPath)!, "Certificates");
            TentacleListeningPfxPath = Path.Combine(directory, "TentacleListening.pfx");
#pragma warning disable SYSLIB0057
            TentacleListening = new X509Certificate2(TentacleListeningPfxPath);
#pragma warning restore SYSLIB0057
            TentacleListeningPublicThumbprint = TentacleListening.Thumbprint;

            OctopusPfxPath = Path.Combine(directory, "Octopus.pfx");
#pragma warning disable SYSLIB0057
            Octopus = new X509Certificate2(OctopusPfxPath);
#pragma warning restore SYSLIB0057
            OctopusPublicThumbprint = Octopus.Thumbprint;

            TentaclePollingPfxPath = Path.Combine(directory, "TentaclePolling.pfx");
#pragma warning disable SYSLIB0057
            TentaclePolling = new X509Certificate2(TentaclePollingPfxPath);
#pragma warning restore SYSLIB0057
            TentaclePollingPublicThumbprint = TentaclePolling.Thumbprint;
            
            WrongPfxPath = Path.Combine(directory, "WrongCert.pfx");
#pragma warning disable SYSLIB0057
            Wrong = new X509Certificate2(WrongPfxPath);
#pragma warning restore SYSLIB0057
            WrongPublicThumbprint = Wrong.Thumbprint;

            sslPfxPath = Path.Combine(directory, "Ssl.pfx");
#pragma warning disable SYSLIB0057
            Ssl = new X509Certificate2(sslPfxPath, "password");
#pragma warning restore SYSLIB0057
            SslThumbprint = Ssl.Thumbprint;
        }
    }
}