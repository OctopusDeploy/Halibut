using System;
using System.Security.Cryptography.X509Certificates;
using System.IO;
using System.Reflection;

namespace Halibut.Tests
{
    public class Certificates
    {
        public static readonly X509Certificate2 TentacleListening;
        public static readonly string TentacleListeningPublicThumbprint;
        public static readonly X509Certificate2 Octopus;
        public static readonly string OctopusPublicThumbprint;
        public static readonly X509Certificate2 TentaclePolling;
        public static readonly string TentaclePollingPublicThumbprint;
        public static readonly X509Certificate2 Ssl;
        public static readonly string SslThumbprint;

        static Certificates()
        {
            //jump through hoops to find certs because the nunit test runner is messing with directories
#if NET40
                var directory = Path.Combine(Path.GetDirectoryName(new Uri(typeof(Certificates).Assembly.CodeBase).LocalPath), "Certificates");
#else
                var directory = Path.Combine(Path.GetDirectoryName(new Uri(typeof(Certificates).GetTypeInfo().Assembly.CodeBase).LocalPath), "Certificates");
#endif
            TentacleListening = new X509Certificate2(Path.Combine(directory, "TentacleListening.pfx"));
            TentacleListeningPublicThumbprint = TentacleListening.Thumbprint;
            Octopus = new X509Certificate2(Path.Combine(directory, "Octopus.pfx"));
            OctopusPublicThumbprint = Octopus.Thumbprint;
            TentaclePolling = new X509Certificate2(Path.Combine(directory, "TentaclePolling.pfx"));
            TentaclePollingPublicThumbprint = TentaclePolling.Thumbprint;
            Ssl = new X509Certificate2(Path.Combine(directory, "Ssl.pfx"), "password");
            SslThumbprint = Ssl.Thumbprint;
        }
    }
}