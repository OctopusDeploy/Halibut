using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;

namespace Halibut.CertificateGenerator
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine("Usage:  Halibut.CertificateGenerator.exe CN=<name> <output-file>");
                return -1;
            }

            string name = args[0];
            string file = args[1];

            X509Certificate2 certificate = CertificateGenerator.Generate(name);

            File.WriteAllBytes(file, certificate.Export(X509ContentType.Pkcs12, (string) null));

            return 0;
        }
    }
}