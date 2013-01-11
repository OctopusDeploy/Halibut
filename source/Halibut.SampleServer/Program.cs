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
		public static void Main (string[] args)
		{
		    var certificate = new X509Certificate2("HalibutServer.pfx");

            var endPoint = new IPEndPoint(IPAddress.Any, 8433);

            var server = new HalibutServer(endPoint, certificate);
            server.Services.Register<ICalculatorService, CalculatorService>();
            server.Options.ClientCertificateValidator = ValidateClientCertificate;
            server.Start();

            Console.WriteLine("Server listening on port 8433...");
		    Console.ReadLine();
		}

	    static CertificateValidationResult ValidateClientCertificate(X509Certificate2 clientcertificate)
	    {
	        return CertificateValidationResult.Valid;
	    }
	}
}
