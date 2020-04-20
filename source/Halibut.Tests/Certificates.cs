using System;
using System.Security.Cryptography.X509Certificates;
using System.IO;
using System.Reflection;

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
        public static X509Certificate2 Ssl;
        public static string SslThumbprint;
        public static X509Certificate2 Router;
        public static string RouterPublicThumbprint;

        static Certificates()
        {
            //jump through hoops to find certs because the nunit test runner is messing with directories
            var directory = Path.Combine(Path.GetDirectoryName(new Uri(typeof(Certificates).Assembly.CodeBase).LocalPath), "Certificates");
            TentacleListening = new X509Certificate2(Path.Combine(directory, "TentacleListening.pfx"));
            TentacleListeningPublicThumbprint = TentacleListening.Thumbprint;
            Octopus = new X509Certificate2(Path.Combine(directory, "Octopus.pfx"));
            OctopusPublicThumbprint = Octopus.Thumbprint;
            TentaclePolling = new X509Certificate2(Path.Combine(directory, "TentaclePolling.pfx"));
            TentaclePollingPublicThumbprint = TentaclePolling.Thumbprint;
            Ssl = new X509Certificate2(Path.Combine(directory, "Ssl.pfx"), "password");
            SslThumbprint = Ssl.Thumbprint;
            Router = new X509Certificate2(Path.Combine(directory, "Router.pfx"));
            RouterPublicThumbprint = Router.Thumbprint;
        }
    }
}