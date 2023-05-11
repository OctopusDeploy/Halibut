using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Halibut;
using Halibut.ServiceModel;

namespace OldMate
{
    public class Program
    {

        
        public static int Main(string[] args)
        {

            var tentacleCertPath = Environment.GetEnvironmentVariable("tentaclecertpath");
            Console.WriteLine($"Using tentacle cert path: {tentacleCertPath}");
            var TentacleCert = new X509Certificate2(tentacleCertPath);

            var octopusThumbprint = Environment.GetEnvironmentVariable("octopusthumbprint");
            Console.WriteLine($"Using octopus thumbprint: {octopusThumbprint}");

            var addressToPoll = Environment.GetEnvironmentVariable("octopusservercommsport");
            Console.WriteLine($"Will poll: {addressToPoll}");
            
            var services = new DelegateServiceFactory();
            services.Register<IEchoService>(() => new EchoService());
            using (var tentaclePolling = new HalibutRuntime(services, TentacleCert))
            {
                tentaclePolling.Poll(new Uri("poll://SQ-TENTAPOLL"), new ServiceEndPoint(new Uri(addressToPoll), octopusThumbprint));
                Thread.Sleep(1000000);
            }
            return 1;
        }

        
    }
}