using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.ServiceModel;
using Halibut.Tests.Support;
using Halibut.Tests.TestServices;
using Halibut.Tests.Util;

namespace Halibut.Tests.BackwardsCompatibility.Util
{
    /// <summary>
    /// Used to test old versions of a client talking to a new version of service.
    /// In this case the client is run out of process, and talks to a service running
    /// in this process.
    /// </summary>
    public class PreviousClientVersionAndServiceBuilder
    {
        readonly ServiceConnectionType serviceConnectionType;
        readonly CertAndThumbprint serviceCertAndThumbprint;
        readonly CertAndThumbprint clientCertAndThumbprint = CertAndThumbprint.Octopus;
        string version = "5.0.429";

        IEchoService echoService = new EchoService();

        PreviousClientVersionAndServiceBuilder(ServiceConnectionType serviceConnectionType, CertAndThumbprint serviceCertAndThumbprint)
        {
            this.serviceConnectionType = serviceConnectionType;
            this.serviceCertAndThumbprint = serviceCertAndThumbprint;
        }

        public static PreviousClientVersionAndServiceBuilder WithPollingService()
        {
            return new PreviousClientVersionAndServiceBuilder(ServiceConnectionType.Polling, CertAndThumbprint.TentaclePolling);
        }

        public static PreviousClientVersionAndServiceBuilder WithListeningService()
        {
            return new PreviousClientVersionAndServiceBuilder(ServiceConnectionType.Listening, CertAndThumbprint.TentacleListening);
        }

        public static PreviousClientVersionAndServiceBuilder ForServiceConnectionType(ServiceConnectionType connectionType)
        {
            switch (connectionType)
            {
                case ServiceConnectionType.Polling:
                    return WithPollingService();
                case ServiceConnectionType.Listening:
                    return WithListeningService();
                default:
                    throw new ArgumentOutOfRangeException(nameof(connectionType), connectionType, null);
            }
        }

        public PreviousClientVersionAndServiceBuilder WithClientVersion(string version)
        {
            this.version = version;
            return this;
        }

        public PreviousClientVersionAndServiceBuilder WithEchoServiceService(IEchoService echoService)
        {
            this.echoService = echoService;
            return this;
        }

        public async Task<ClientAndService> Build()
        {
            
            // We need to build both a Client and a service.
            if (version == null) throw new Exception("The version of the service must be set.");
            var client = new HalibutRuntime(clientCertAndThumbprint.Certificate2);
            client.Trust(serviceCertAndThumbprint.Thumbprint);

            var realService2 = ClientServiceBuilder.ForMode(serviceConnectionType)
                .WithService<IEchoService>(() => echoService)
                .Build();
            
            var tentacle = new HalibutRuntimeBuilder()
                .WithServiceFactory(new DelegateServiceFactory().Register<IEchoService>(() => echoService))
                .WithServerCertificate(serviceCertAndThumbprint.Certificate2)
                .WithLogFactory(new TestContextLogFactory("Tentacle"))
                .Build();

            Uri proxyServiceUri;
            ProxyHalibutTestBinaryRunner.RoundTripRunningOldHalibutBinary runningOldHalibutBinary;
            if (serviceConnectionType == ServiceConnectionType.Polling)
            {
                var clientListenPort = client.Listen();
                runningOldHalibutBinary = await new ProxyHalibutTestBinaryRunner(serviceConnectionType, clientListenPort, clientCertAndThumbprint, serviceCertAndThumbprint, new Uri("poll://SQ-TENTAPOLL"), version).Run();
                proxyServiceUri = new Uri("poll://SQ-TENTAPOLL");
                // TODO support this.
                
                tentacle.Poll(proxyServiceUri, new ServiceEndPoint(new Uri("https://localhost:" + runningOldHalibutBinary.proxyClientListenPort), clientCertAndThumbprint.Thumbprint));
            }
            else
            {
                var port = tentacle.Listen();
                tentacle.Trust(clientCertAndThumbprint.Thumbprint);
                runningOldHalibutBinary = await new ProxyHalibutTestBinaryRunner(serviceConnectionType, null, clientCertAndThumbprint, serviceCertAndThumbprint, new Uri("https://localhost:" + port), version).Run();
                proxyServiceUri = new Uri("https://localhost:" + runningOldHalibutBinary.serviceListenPort);
            }

            return new ClientAndService(client, runningOldHalibutBinary, proxyServiceUri, serviceCertAndThumbprint, tentacle);
        }

        public class ClientAndService : IDisposable
        {
            readonly HalibutRuntime octopus;
            readonly ProxyHalibutTestBinaryRunner.RoundTripRunningOldHalibutBinary runningOldHalibutBinary;
            readonly Uri serviceUri;
            readonly CertAndThumbprint serviceCertAndThumbprint; // for creating a client
            readonly HalibutRuntime tentacle;

            public ClientAndService(HalibutRuntime octopus,
                ProxyHalibutTestBinaryRunner.RoundTripRunningOldHalibutBinary runningOldHalibutBinary,
                Uri serviceUri,
                CertAndThumbprint serviceCertAndThumbprint,
                HalibutRuntime tentacle)
            {
                this.octopus = octopus;
                this.runningOldHalibutBinary = runningOldHalibutBinary;
                this.serviceUri = serviceUri;
                this.serviceCertAndThumbprint = serviceCertAndThumbprint;
                this.tentacle = tentacle;
            }

            public TService CreateClient<TService>()
            {
                return CreateClient<TService>(s => { }, CancellationToken.None);
            }

            public TService CreateClient<TService>(Action<ServiceEndPoint> modifyServiceEndpoint)
            {
                return CreateClient<TService>(modifyServiceEndpoint, CancellationToken.None);
            }

            public TService CreateClient<TService>(Action<ServiceEndPoint> modifyServiceEndpoint, CancellationToken cancellationToken)
            {
                var serviceEndpoint = new ServiceEndPoint(serviceUri, serviceCertAndThumbprint.Thumbprint);
                modifyServiceEndpoint(serviceEndpoint);
                return octopus.CreateClient<TService>(serviceEndpoint, cancellationToken);
            }
            
            public TClientService CreateClient<TService, TClientService>(Action<ServiceEndPoint> modifyServiceEndpoint)
            {
                var serviceEndpoint = new ServiceEndPoint(serviceUri, serviceCertAndThumbprint.Thumbprint);
                modifyServiceEndpoint(serviceEndpoint);
                return octopus.CreateClient<TService, TClientService>(serviceEndpoint);
            }

            public void Dispose()
            {
                octopus.Dispose();
                runningOldHalibutBinary.Dispose();
                tentacle.Dispose();
            }
        }
    }
}