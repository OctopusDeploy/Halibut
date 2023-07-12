using System;
using System.Threading.Tasks;
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
        public static async Task Run()
        {
            var serviceCert = SettingsHelper.GetServiceCertificate();
            var clientCert = SettingsHelper.GetClientCertificate();
            Console.WriteLine($"This should be bob: {clientCert}");

            var serviceConnectionType = SettingsHelper.GetServiceConnectionType();
            var proxyDetails = SettingsHelper.GetProxyDetails();

            string addressToPoll = null;
            if (serviceConnectionType == ServiceConnectionType.Polling)
            {
                addressToPoll = SettingsHelper.GetSetting("octopusservercommsport");
                Console.WriteLine($"Will poll: {addressToPoll}");
            }

            string serviceAddressToConnectTo = null;
            if (serviceConnectionType == ServiceConnectionType.Listening)
            {
                serviceAddressToConnectTo = SettingsHelper.GetSetting("realServiceListenAddress");
                Console.WriteLine($"Will forward request to: {serviceAddressToConnectTo}");
            }

            // A Halibut Runtime which is the previous version of Halibut the Test is trying to test.
            // This will communicate with the Halibut Service created in the Test Process (the Tentacle were trying to test against)
            using (var client = new HalibutRuntimeBuilder()
                       .WithServerCertificate(clientCert)
                       .WithLogFactory(new TestContextLogFactory("ProxyClient", SettingsHelper.GetHalibutLogLevel()))
                       .Build())
            {
                Console.WriteLine("Next line should be: 36F35047CE8B000CF4C671819A2DD1AFCDE3403D");
                Console.WriteLine("Client will trust: " + serviceCert.Thumbprint);
                client.Trust(serviceCert.Thumbprint);

                Uri realServiceUri;
                switch (serviceConnectionType)
                {
                    case ServiceConnectionType.Polling:
                        var clientPollingListeningPort = client.Listen();
                        Console.WriteLine("Polling listener is listening on port: " + clientPollingListeningPort);
                        realServiceUri = new Uri("poll://SQ-TENTAPOLL");
                        break;
                    case ServiceConnectionType.Listening:
                        realServiceUri = new Uri(serviceAddressToConnectTo!);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                var realServiceEndpoint = new ServiceEndPoint(realServiceUri, serviceCert.Thumbprint, proxyDetails);

                var services = ServiceFactoryFactory.CreateProxyingServicesServiceFactory(client, realServiceEndpoint);
                using (var proxyService = new HalibutRuntimeBuilder()
                           .WithServiceFactory(services)
                           .WithServerCertificate(serviceCert)
                           .WithLogFactory(new TestContextLogFactory("ProxyService", SettingsHelper.GetHalibutLogLevel()))
                           .Build())
                {
                    switch (serviceConnectionType)
                    {
                        case ServiceConnectionType.Polling:
                            proxyService.Poll(new Uri("poll://SQ-TENTAPOLL"), new ServiceEndPoint(new Uri(addressToPoll!), clientCert.Thumbprint));
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

                    await Console.Out.FlushAsync();

                    // Run until the Program is terminated
                    await Task.Delay(-1);
                }
            }
        }
    }
}