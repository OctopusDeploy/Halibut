using System;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using Halibut.SampleContracts;
using Halibut.ServiceModel;

namespace Halibut.SampleServer
{
    class Program
    {
        public static void Main(string[] args)
        {
            Console.Title = "Halibut Server";
            var certificate = new X509Certificate2("HalibutServer.pfx");

            var endPoint = new IPEndPoint(IPAddress.Any, 8433);

            var services = new DelegateServiceFactory();
            services.Register<ICalculatorService>(() => new CalculatorService());

            using (var server = new HalibutRuntime(services, certificate))
            {
                server.Listen(endPoint);
                server.Trust("2074529C99D93D5955FEECA859AEAC6092741205");

                Console.WriteLine("Server listening on port 8433. Type 'exit' to quit, or 'cls' to clear...");

                while (true)
                {
                    var line = Console.ReadLine();
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
        }
    }
}