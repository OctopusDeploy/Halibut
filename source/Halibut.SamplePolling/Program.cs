using System;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using Halibut.SampleLoadTest;
using Halibut.ServiceModel;
using Serilog;

namespace Halibut.SamplePolling
{
    class Program
    {
        static readonly X509Certificate2 ClientCertificate = new X509Certificate2("HalibutClient.pfx");
        static readonly X509Certificate2 ServerCertificate = new X509Certificate2("HalibutServer.pfx");
        const string PollUrl = "poll://SQ-TENTAPOLL";

        static void Main()
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.ColoredConsole()
                .CreateLogger();

            var services = new DelegateServiceFactory();
            services.Register<ICalculatorService>(() => new CalculatorService());

            using (var client = new HalibutRuntime(new NullServiceFactory(), ClientCertificate))
            using (var server = new HalibutRuntime(services, ServerCertificate))
            {
                var octopusPort = client.Listen();
                client.Trust(ServerCertificate.Thumbprint);

                server.Poll(new Uri(PollUrl), new ServiceEndPoint(new Uri("https://localhost:" + octopusPort), ClientCertificate.Thumbprint));

                var calculator = client.CreateClient<ICalculatorService>(PollUrl, ServerCertificate.Thumbprint);
                var result = calculator.Add(12, 18);
                Debug.Assert(result == 30);
            }
            Console.ReadKey();
        }
    }
}