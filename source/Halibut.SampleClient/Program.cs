using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Halibut.Client;
using Halibut.SampleContracts;

namespace Halibut.SampleClient
{
	class Program
	{
		public static void Main(string[] args)
        {
            Console.Title = "Halibut Client";

		    var hostName = args.FirstOrDefault() ?? "localhost";

            var certificate = new X509Certificate2("HalibutClient.pfx");

		    var client = new HalibutClient(certificate);
            var calculator = client.Create<ICalculatorService>(new Uri("rpc://" + hostName + ":8433/"), "EF3A7A69AFE0D13130370B44A228F5CD15C069BC");
		    var result = calculator.Add(12, 18);

            Console.WriteLine("12 + 18 = " + result);
		}
	}
}
