using System;
using System.Threading.Tasks;
using Halibut.Tests.Support;
using Halibut.TestUtils.SampleProgram.Base.LogUtils;
using Halibut.TestUtils.SampleProgram.Base.Services;

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
            var serviceConnectionType = SettingsHelper.GetServiceConnectionType();
            var serviceCert = SettingsHelper.GetServiceCertificate();
            var clientCert = SettingsHelper.GetClientCertificate();
            var proxyDetails = SettingsHelper.GetProxyDetails();

            string proxyClientAddressToPoll = null;
            if (serviceConnectionType is ServiceConnectionType.Polling or ServiceConnectionType.PollingOverWebSocket)
            {
                proxyClientAddressToPoll = SettingsHelper.GetSetting("octopusservercommsport");
                Console.WriteLine($"Proxy Service will Poll: {proxyClientAddressToPoll}");
            }

            string serviceAddressToConnectTo = null;
            if (serviceConnectionType == ServiceConnectionType.Listening)
            {
                serviceAddressToConnectTo = SettingsHelper.GetSetting("realServiceListenAddress");
                Console.WriteLine($"Proxy Service will forward request to: {serviceAddressToConnectTo}");
            }

            // A Halibut Runtime Client which is the previous version of Halibut the Test is trying to test.
            // This will communicate with the Halibut Service created in the Test Process (the Tentacle were trying to test against)
            using (var client = new HalibutRuntimeBuilder()
                       .WithServerCertificate(clientCert)
                       .WithLogFactory(new TestContextLogFactory("PreviousVersionOfClient", SettingsHelper.GetHalibutLogLevel()))
                       .Build())
            {
                Console.WriteLine("Client will trust: " + serviceCert.Thumbprint);
                client.Trust(serviceCert.Thumbprint);

                ServiceEndPoint realServiceEndpoint;
                switch (serviceConnectionType)
                {
                    case ServiceConnectionType.Polling:
                        var clientPollingListeningPort = client.Listen();
                        // Do not change the log message as it is used by the HalibutTestBinaryRunners
                        Console.WriteLine("Polling listener is listening on port: " + clientPollingListeningPort);
                        realServiceEndpoint = new ServiceEndPoint(new Uri("poll://SQ-TENTAPOLL"), serviceCert.Thumbprint, proxyDetails);
                        break;
                    case ServiceConnectionType.PollingOverWebSocket:
                        var webSocketListeningPort = TcpPortHelper.FindFreeTcpPort();
                        var webSocketPath = SettingsHelper.GetSetting("websocketpath");
                        var webSocketListeningUrl = $"https://+:{webSocketListeningPort}/{webSocketPath}";

                        client.ListenWebSocket(webSocketListeningUrl);
                        Console.WriteLine($"WebSocket Polling listener is listening on: {webSocketListeningUrl}");
                        // Do not change the log message as it is used by the HalibutTestBinaryRunners
                        Console.WriteLine("Polling listener is listening on port: " + webSocketListeningPort);
                        realServiceEndpoint = new ServiceEndPoint(new Uri("poll://SQ-TENTAPOLL"), serviceCert.Thumbprint, proxyDetails);
                        break;
                    case ServiceConnectionType.Listening:
                        realServiceEndpoint = new ServiceEndPoint(new Uri(serviceAddressToConnectTo!), serviceCert.Thumbprint, proxyDetails);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                // A Halibut Runtime which will be the Service that proxies requests to the real Service (Tentacle) in the Test CLR
                // using an old version of Halibut as the Client which is the thing we are trying to test
                using (var proxyService = new HalibutRuntimeBuilder()
                           .WithServiceFactory(ServiceFactoryFactory.CreateProxyingServicesServiceFactory(client, realServiceEndpoint))
                           .WithServerCertificate(serviceCert)
                           .WithLogFactory(new TestContextLogFactory("ProxyService", SettingsHelper.GetHalibutLogLevel()))
                           .Build())
                {
                    switch (serviceConnectionType)
                    {
                        case ServiceConnectionType.Polling:
                        case ServiceConnectionType.PollingOverWebSocket:
                            // PollingOverWebsockets exposes a proxy client that is just Polling for simplicity
                            proxyService.Poll(new Uri("poll://SQ-TENTAPOLL"), new ServiceEndPoint(new Uri(proxyClientAddressToPoll!), clientCert.Thumbprint));
                            break;
                        case ServiceConnectionType.Listening:
                            var proxyServiceListeningPort = proxyService.Listen();
                            // Do not change the log message as it is used by the HalibutTestBinaryRunners
                            Console.WriteLine($"Listening on port: {proxyServiceListeningPort}");
                            proxyService.Trust(clientCert.Thumbprint);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    Console.WriteLine("RunningAndReady");
                    Console.WriteLine("Will Now sleep");

                    await Console.Out.FlushAsync();

                    // Run until the Program is terminated
                    await StayAliveUntilHelper.WaitUntilSignaledToDie();
                }
            }
        }
    }
}