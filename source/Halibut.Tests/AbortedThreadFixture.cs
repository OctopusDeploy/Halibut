using System;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Halibut.ServiceModel;
using Halibut.Transport;
using NUnit.Framework;
using Serilog;

namespace Halibut.Tests
{
    [TestFixture]
    public class AbortedThreadFixture
    {
        static readonly X509Certificate2 OctopusCertificate = Certificates.Octopus;
        static readonly X509Certificate2 TentacleCertificate = Certificates.TentaclePolling;
        const string PollUrl = "poll://SQ-TENTAPOLL";

        [Test]
        public void TestTheThing()
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.ColoredConsole()
                .CreateLogger();

            var services = new DelegateServiceFactory();
            services.Register<ICalculatorService>(() => new ThreadKillingCalculatorService());

            using (var octopus = new HalibutRuntime(services, OctopusCertificate))
            using (var tentacle = new HalibutRuntime(services, TentacleCertificate))
            {
                var octopusPort = octopus.Listen();
                octopus.Trust(TentacleCertificate.Thumbprint);

                tentacle.Poll(new Uri(PollUrl), new ServiceEndPoint(new Uri("https://localhost:" + octopusPort), OctopusCertificate.Thumbprint));

                var calculator = octopus.CreateClient<ICalculatorService>(PollUrl, TentacleCertificate.Thumbprint);
                var result = calculator.Add(12, 18);
                Debug.Assert(result == 30);
            }
            Console.ReadKey();
        }
    }

    class ThreadKillingCalculatorService : ICalculatorService
    {
        public ThreadKillingCalculatorService()
        {
        }

        public long Add(long a, long b)
        {
            // Thread.CurrentThread.Abort();
            PollingClient.thread.Abort();
            throw new NotImplementedException();
        }
    }
}