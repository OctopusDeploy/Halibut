using System;
using System.Security.Cryptography.X509Certificates;

namespace Halibut.Tests
{
    public class Certificates
    {
        public static X509Certificate2 Alice;
        public static string AlicePublicThumbprint;
        public static X509Certificate2 Bob;
        public static string BobPublicThumbprint;
        public static X509Certificate2 Eve;
        public static string EvePublicThumbprint;

        static Certificates()
        {
            Alice = new X509Certificate2("Certificates\\HalibutAlice.pfx");
            AlicePublicThumbprint = Alice.Thumbprint;
            Bob = new X509Certificate2("Certificates\\HalibutBob.pfx");
            BobPublicThumbprint = Bob.Thumbprint;
            Eve = new X509Certificate2("Certificates\\HalibutEve.pfx");
            EvePublicThumbprint = Eve.Thumbprint;
        }
    }
}