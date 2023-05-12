using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Halibut.ServiceModel;
using Halibut.TestUtils.SampleProgram.v5_0_429;

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
            if (serviceConnectionType == ServiceConnectionType.Polling)
            {
                addressToPoll = Environment.GetEnvironmentVariable("octopusservercommsport");
                Console.WriteLine($"Will poll: {addressToPoll}");
            }
            

            var services = new DelegateServiceFactory();
            services.Register<IEchoService>(() => new EchoService());
            using (var tentaclePolling = new HalibutRuntime(services, TentacleCert))
            {
                switch (serviceConnectionType)
                {
                    case ServiceConnectionType.Polling:
                        tentaclePolling.Poll(new Uri("poll://SQ-TENTAPOLL"), new ServiceEndPoint(new Uri(addressToPoll), octopusThumbprint));
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
        
        public static ServiceConnectionType ServiceConnectionTypeFromString(string s) {
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
        Listening
        
    }
}