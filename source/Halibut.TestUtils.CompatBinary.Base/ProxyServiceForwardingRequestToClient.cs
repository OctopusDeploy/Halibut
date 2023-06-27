using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Halibut.ServiceModel;
using Halibut.TestUtils.SampleProgram.Base.LogUtils;

namespace Halibut.TestUtils.SampleProgram.Base
{
    /// <summary>
    /// Creates a Proxy Service which forwards requests to another halibut client, which itself
    /// makes requests to a service such as one running in the test CLR.
    /// 
    /// This is used when testing the compatibility of a old client with latest service, since this
    /// has the old client which is making requests to the new service in the test CLR.
    /// The Proxy 
    /// </summary>
    public class ProxyServiceForwardingRequestToClient
    {
        public static int Run(string[] args)
        {
            var tentacleCertPath = Environment.GetEnvironmentVariable("tentaclecertpath");
            Console.WriteLine($"Using tentacle cert path: {tentacleCertPath}");
            var serviceCert = new X509Certificate2(tentacleCertPath);
            
            Console.WriteLine("Tentacle/service cert details " + serviceCert);

            
            var octopusCertPath = Environment.GetEnvironmentVariable("octopuscertpath");
            Console.WriteLine($"Using octopus cert path: {octopusCertPath}");
            var clientCert = new X509Certificate2(octopusCertPath);
            Console.WriteLine("Octopus/Client cert details " + clientCert);
            

            ServiceConnectionType serviceConnectionType = ServiceConnectionTypeFromString(Environment.GetEnvironmentVariable("ServiceConnectionType"));
            string addressToPoll = null;
            if (serviceConnectionType == ServiceConnectionType.Polling)
            {
                addressToPoll = Environment.GetEnvironmentVariable("octopusservercommsport");
                Console.WriteLine($"Will poll: {addressToPoll}");
            }

            string serivceAddressToConnectTo = null; 
            if (serviceConnectionType == ServiceConnectionType.Listening)
            {
                serivceAddressToConnectTo = Environment.GetEnvironmentVariable("realServiceListenAddress");
                Console.WriteLine($"Will forward request to: {serivceAddressToConnectTo}");
            }
            
            // Create a client which will send RPC calls to the service in the Test CLR.
            Console.WriteLine("This should be bob:");
            Console.WriteLine(clientCert.ToString());
            using (var client = new HalibutRuntimeBuilder()
                       .WithServerCertificate(clientCert)
                       .WithLogFactory(new TestContextLogFactory("ProxyClient"))
                       .Build())
            {
                // 
                Console.WriteLine("Next line should be: 36F35047CE8B000CF4C671819A2DD1AFCDE3403D");
                Console.WriteLine("Client will trust: " + serviceCert.Thumbprint);
                client.Trust(serviceCert.Thumbprint);

                
                Uri realServiceUri = null;
                switch (serviceConnectionType)
                {
                    case ServiceConnectionType.Polling:
                        var clientPollingListeningPort = client.Listen();
                        Console.WriteLine("Polling listener is listening on port: " + clientPollingListeningPort);
                        
                        // This needs to be done within the CLR.
                        //tentaclePolling.Poll(new Uri("poll://SQ-TENTAPOLL"), new ServiceEndPoint(new Uri(addressToPoll), octopusThumbprint));
                        realServiceUri = new Uri("poll://SQ-TENTAPOLL");
                        break;
                    case ServiceConnectionType.Listening:
                        realServiceUri = new Uri(serivceAddressToConnectTo);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                var realServiceEndpoint = new ServiceEndPoint(realServiceUri, serviceCert.Thumbprint);

                var forwardingEchoService = client.CreateClient<IEchoService>(realServiceEndpoint);
                
                // We can only test in listening mode since in polling not everything is setup yet.
                // Console.WriteLine("Testing can communicate with service from external proxy client");
                // forwardingEchoService.SayHello("Hello");
                // Console.WriteLine("Testing passed, we can talk from the external binary back to the service");


                // Connects as service to the client.
                var services = new DelegateServiceFactory();
                services.Register<IEchoService>(() => new DelegateEchoService(forwardingEchoService));
                using (var proxyService = new HalibutRuntimeBuilder()
                           .WithServiceFactory(services)
                           .WithServerCertificate(serviceCert)
                           .WithLogFactory(new TestContextLogFactory("ProxyService"))
                           .Build())
                {
                    switch (serviceConnectionType)
                    {
                        case ServiceConnectionType.Polling:
                            proxyService.Poll(new Uri("poll://SQ-TENTAPOLL"), new ServiceEndPoint(new Uri(addressToPoll), clientCert.Thumbprint));
                            break;
                        case ServiceConnectionType.Listening:
                            var port = proxyService.Listen();
                            Console.WriteLine($"Listening on port: {port}");
                            proxyService.Trust(clientCert.Thumbprint);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    Console.WriteLine("RunningAndReady");
                    Console.WriteLine("Will Now sleep");
                    Console.Out.Flush();
                    Thread.Sleep(1000000);
                }
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
}