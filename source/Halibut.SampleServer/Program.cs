using System;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using Halibut.SampleContracts;
using Halibut.ServiceModel;
using Serilog;

namespace Halibut.SampleServer
{
    class Program
    {
        const string SslCertificateThumbprint = "6E5C6492129B75A4C83E1A23797AF6344092E5C2"; // For WebSockets. This is different to the internally configured thumbprint
        public static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.ColoredConsole()
                .MinimumLevel.Warning()
                .CreateLogger();

            Console.Title = "Halibut Server";
            var certificate = new X509Certificate2("HalibutServer.pfx");

            var endPoint = new IPEndPoint(IPAddress.IPv6Any, 8433);

            var services = new DelegateServiceFactory();
            services.Register<ICalculatorService>(() => new CalculatorService());

            using (var server = new HalibutRuntime(services, certificate))
            {
                //Although this is the "Server" because it is the thing handling a request
                //in Octopus terms, this would be the Tentacle, being asked to do some work

                //Begin Listening Setup
                //server.Listen(endPoint);
                //server.Trust("2074529C99D93D5955FEECA859AEAC6092741205");
                //End Listening Setup

                //Begin Polling Setup
                //server.Poll(new Uri("poll://SQ-TENTAPOLL"), new ServiceEndPoint(new Uri("https://localhost:8433"), "2074529C99D93D5955FEECA859AEAC6092741205"));
                //End Polling Setup

                //Begin WebSocket Polling Setup
                
                server.Poll(new Uri("poll://SQ-TENTAPOLL"), new ServiceEndPoint(new Uri("wss://localhost:8433/Halibut"), SslCertificateThumbprint));
                //End WebSocket Polling Setup

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