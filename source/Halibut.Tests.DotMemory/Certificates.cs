using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;

namespace Halibut.Tests.DotMemory
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
        
        public static X509Certificate2 Ssl;
        public static string SslThumbprint;

        static Certificates()
        {
            //jump through hoops to find certs because the nunit test runner is messing with directories
            var directory = Path.Combine(Path.GetDirectoryName(new Uri(typeof(Certificates).Assembly.Location).LocalPath)!, "Certificates");
            TentacleListeningPfxPath = Path.Combine(directory, "TentacleListening.pfx");
#pragma warning disable SYSLIB0057 // obsolete ctor; kept for identical behaviour on net48 and net10
            TentacleListening = new X509Certificate2(TentacleListeningPfxPath);
#pragma warning restore SYSLIB0057
            TentacleListeningPublicThumbprint = TentacleListening.Thumbprint;

            OctopusPfxPath = Path.Combine(directory, "Octopus.pfx");
#pragma warning disable SYSLIB0057 // obsolete ctor; kept for identical behaviour on net48 and net10
            Octopus = new X509Certificate2(OctopusPfxPath);
#pragma warning restore SYSLIB0057
            OctopusPublicThumbprint = Octopus.Thumbprint;
            
            TentaclePollingPfxPath = Path.Combine(directory, "TentaclePolling.pfx");
#pragma warning disable SYSLIB0057 // obsolete ctor; kept for identical behaviour on net48 and net10
            TentaclePolling = new X509Certificate2(TentaclePollingPfxPath);
#pragma warning restore SYSLIB0057
            TentaclePollingPublicThumbprint = TentaclePolling.Thumbprint;
#pragma warning disable SYSLIB0057 // obsolete ctor; kept for identical behaviour on net48 and net10
            Ssl = new X509Certificate2(Path.Combine(directory, "Ssl.pfx"), "password");
#pragma warning restore SYSLIB0057
            SslThumbprint = Ssl.Thumbprint;
        }
    }
}