using System;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using Halibut.SampleContracts;
using Halibut.Server;
using Halibut.Server.Security;

namespace Halibut.SampleServer
{
    class Program
    {
        public static void Main(string[] args)
        {
            Console.Title = "Halibut Server";
            var certificate = new X509Certificate2("HalibutServer.pfx");

            var endPoint = new IPEndPoint(IPAddress.Any, 8433);

            var server = new HalibutServer(endPoint, certificate);
            server.Services.Register<ICalculatorService, CalculatorService>();
            server.Options.ClientCertificateValidator = ValidateClientCertificate;
            server.Start();

            Console.WriteLine("Server listening on port 8433...");

            while (true)
            {
                string line = Console.ReadLine();
                if (string.Equals("cls", line, StringComparison.OrdinalIgnoreCase))
                {
                    Console.Clear();
                    continue;
                }

                if (string.Equals("q", line, StringComparison.OrdinalIgnoreCase))
                    return;

                if (string.Equals("exit", line, StringComparison.OrdinalIgnoreCase))
                    return;

                Console.WriteLine("Unknown command. Enter 'q' to quit.");
            }
        }

        static CertificateValidationResult ValidateClientCertificate(X509Certificate2 clientcertificate)
        {
            return clientcertificate.Thumbprint == "2074529C99D93D5955FEECA859AEAC6092741205"
                       ? CertificateValidationResult.Valid
                       : CertificateValidationResult.Rejected;
        }
    }
}