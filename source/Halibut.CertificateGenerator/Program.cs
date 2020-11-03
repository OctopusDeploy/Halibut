using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;

namespace Halibut.CertificateGenerator
{
    class Program
    {
        static int Main(string[] args)
        {
#if NETCOREAPP
            throw new NotSupportedException("Please refer to the README for alternatives that will run on this platform");
#else
            if (args.Length != 2)
            {
                Console.WriteLine("Usage:  Halibut.CertificateGenerator.exe CN=<name> <output-file>");
                return -1;
            }

            var name = args[0];
            var file = args[1];

            var certificate = CertificateGenerator.Generate(name);

            File.WriteAllBytes(file, certificate.Export(X509ContentType.Pkcs12, (string) null));

            return 0;
#endif
        }
    }
}