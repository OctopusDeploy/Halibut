using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Halibut.SampleContracts;

namespace Halibut.SampleClient
{
    class Program
    {
        public static void Main(string[] args)
        {
            Console.Title = "Halibut Client";

            var hostName = args.FirstOrDefault() ?? "localhost";
            var port = args.Skip(1).FirstOrDefault() ?? "8433";

            var certificate = new X509Certificate2("HalibutClient.pfx");

            using (var runtime = new HalibutRuntime(certificate))
            {
                var calculator = runtime.CreateClient<ICalculatorService>("https://" + hostName + ":" + port + "/", "EF3A7A69AFE0D13130370B44A228F5CD15C069BC");

                var result = calculator.Add(12, 18);

                Console.WriteLine("12 + 18 = " + result);
            }
        }
    }
}