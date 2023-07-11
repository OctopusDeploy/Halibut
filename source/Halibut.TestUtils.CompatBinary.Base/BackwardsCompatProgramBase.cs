using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Halibut.ServiceModel;

namespace Halibut.TestUtils.SampleProgram.Base
{
    public class BackwardsCompatProgramBase
    {
        public static int Main(string[] args)
        {
            var tentacleCertPath = Environment.GetEnvironmentVariable("tentaclecertpath");
            Console.WriteLine($"Using tentacle cert path: {tentacleCertPath}");
            var TentacleCert = new X509Certificate2(tentacleCertPath);

            var octopusThumbprint = Environment.GetEnvironmentVariable("octopusthumbprint");
            Console.WriteLine($"Using octopus thumbprint: {octopusThumbprint}");

            ServiceConnectionType serviceConnectionType = ServiceConnectionTypeFromString(Environment.GetEnvironmentVariable("ServiceConnectionType"));
            string addressToPoll = null;
            if (serviceConnectionType is ServiceConnectionType.Polling or ServiceConnectionType.PollingOverWebSocket)
            {
                addressToPoll = Environment.GetEnvironmentVariable("octopusservercommsport");
                Console.WriteLine($"Will poll: {addressToPoll}");
            }

            var services = new DelegateServiceFactory();
            services.Register<IEchoService>(() => new EchoService());
            services.Register<ICachingService>(() => new CachingService());
            using (var tentaclePolling = new HalibutRuntime(services, TentacleCert))
            {
                switch (serviceConnectionType)
                {
                    case ServiceConnectionType.Polling:
                        tentaclePolling.Poll(new Uri("poll://SQ-TENTAPOLL"), new ServiceEndPoint(new Uri(addressToPoll), octopusThumbprint));
                        break;
                    case ServiceConnectionType.PollingOverWebSocket:
                        var sslThubprint = Environment.GetEnvironmentVariable("sslthubprint");
                        Console.WriteLine($"Using SSL thumbprint: {sslThubprint}");

                        tentaclePolling.Poll(new Uri("poll://SQ-TENTAPOLL"), new ServiceEndPoint(new Uri(addressToPoll), sslThubprint));
                        break;
                    case ServiceConnectionType.Listening:
                        var port = tentaclePolling.Listen();
                        Console.WriteLine($"Listening on port: {port}");
                        tentaclePolling.Trust(octopusThumbprint);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                Console.WriteLine("RunningAndReady");
                Console.WriteLine("Will Now sleep");
                Console.Out.Flush();
                Thread.Sleep(1000000);
            }

            return 1;
        }

        public static ServiceConnectionType ServiceConnectionTypeFromString(string s)
        {
            if (Enum.TryParse(s, out ServiceConnectionType serviceConnectionType))
            {
                return serviceConnectionType;
            }

            throw new Exception($"Unknown service type '{s}'");
        }
    }

    public enum ServiceConnectionType
    {
        Polling,
        PollingOverWebSocket,
        Listening
    }
}