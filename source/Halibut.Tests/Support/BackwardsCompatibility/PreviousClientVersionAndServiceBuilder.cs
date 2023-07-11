using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.ServiceModel;
using Halibut.Tests.Support;
using Halibut.Tests.Support.BackwardsCompatibility;
using Halibut.Tests.TestServices;
using Halibut.Tests.Util;
using IoC;
using Octopus.TestPortForwarder;

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
        string? version = null;

        IEchoService echoService = new EchoService();
        Func<int, PortForwarder>? portForwarderFactory;

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
        
        public PreviousClientVersionAndServiceBuilder WithPortForwarding(Func<int, PortForwarder> portForwarderFactory)
        {
            this.portForwarderFactory = portForwarderFactory;
            return this;
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

            var tentacle = new HalibutRuntimeBuilder()
                // TODO register the other
                .WithServiceFactory(new DelegateServiceFactory().Register<IEchoService>(() => echoService))
                .WithServerCertificate(serviceCertAndThumbprint.Certificate2)
                .WithLogFactory(new TestContextLogFactory("Tentacle"))
                .Build();

            PortForwarder? portForwarder = null;
            Uri proxyServiceUri;
            ProxyHalibutTestBinaryRunner.RoundTripRunningOldHalibutBinary runningOldHalibutBinary;
            if (serviceConnectionType == ServiceConnectionType.Polling)
            {
                var clientListenPort = client.Listen();
                runningOldHalibutBinary = await new ProxyHalibutTestBinaryRunner(serviceConnectionType, clientListenPort, clientCertAndThumbprint, serviceCertAndThumbprint, new Uri("poll://SQ-TENTAPOLL"), version).Run();
                proxyServiceUri = new Uri("poll://SQ-TENTAPOLL");
                
                portForwarder = portForwarderFactory?.Invoke((int) runningOldHalibutBinary.proxyClientListenPort!);

                var listenPort = portForwarder?.ListeningPort ?? (int)runningOldHalibutBinary.proxyClientListenPort!;
                tentacle.Poll(proxyServiceUri, new ServiceEndPoint(new Uri("https://localhost:" + listenPort), clientCertAndThumbprint.Thumbprint));
            }
            else
            {
                var listenPort = tentacle.Listen();
                tentacle.Trust(clientCertAndThumbprint.Thumbprint);
                portForwarder = portForwarderFactory != null ? portForwarderFactory(listenPort) : null;
                listenPort = portForwarder?.ListeningPort??listenPort;
                runningOldHalibutBinary = await new ProxyHalibutTestBinaryRunner(serviceConnectionType, null, clientCertAndThumbprint, serviceCertAndThumbprint, new Uri("https://localhost:" + listenPort), version).Run();
                proxyServiceUri = new Uri("https://localhost:" + runningOldHalibutBinary.serviceListenPort);
            }

            return new ClientAndService(client, runningOldHalibutBinary, proxyServiceUri, serviceCertAndThumbprint, portForwarder, tentacle);
        }

        public class ClientAndService : IClientAndService
        {
            public HalibutRuntime Octopus { get; }
            readonly ProxyHalibutTestBinaryRunner.RoundTripRunningOldHalibutBinary runningOldHalibutBinary;
            readonly Uri serviceUri;
            readonly CertAndThumbprint serviceCertAndThumbprint; // for creating a client
            readonly PortForwarder? portForwarder;
            readonly HalibutRuntime tentacle;
            
            public PortForwarder? PortForwarder { get; }

            public ClientAndService(HalibutRuntime octopus,
                ProxyHalibutTestBinaryRunner.RoundTripRunningOldHalibutBinary runningOldHalibutBinary,
                Uri serviceUri,
                CertAndThumbprint serviceCertAndThumbprint,
                PortForwarder? portForwarder,
                HalibutRuntime tentacle)
            {
                this.Octopus = octopus;
                this.runningOldHalibutBinary = runningOldHalibutBinary;
                this.serviceUri = serviceUri;
                this.serviceCertAndThumbprint = serviceCertAndThumbprint;
                this.portForwarder = portForwarder;
                this.tentacle = tentacle;
            }

            /// <summary>
            /// Creates a client, which forwards RPC calls on to a proxy in a external process which will make those calls back
            /// to service in the this process.
            /// </summary>
            /// <typeparam name="TService"></typeparam>
            /// <returns></returns>
            public TService CreateClient<TService>(CancellationToken? cancellationToken = null, string? remoteThumbprint = null)
            {
                if (remoteThumbprint == null) remoteThumbprint = serviceCertAndThumbprint.Thumbprint;
                if (remoteThumbprint != serviceCertAndThumbprint.Thumbprint)
                {
                    throw new Exception("Setting the remote thumbprint is unsupported, since it would not actually be passed on to the remote process which holds the actual client under test.");
                }

                if (cancellationToken != null && cancellationToken != CancellationToken.None)
                {
                    throw new Exception("Setting the connect cancellation token to anything other than none is unsupported, since it would not actually be passed on to the remote process which holds the actual client under test.");
                }
                
                var serviceEndpoint = new ServiceEndPoint(serviceUri, serviceCertAndThumbprint.Thumbprint);
                return Octopus.CreateClient<TService>(serviceEndpoint, cancellationToken??CancellationToken.None);
            }

            /// <summary>
            /// This probably never makes sense to be called, since this modifies the client to the proxy client. If a test
            /// wanted to set these it is probably setting them on the wrong client
            /// </summary>
            /// <param name="modifyServiceEndpoint"></param>
            /// <param name="cancellationToken"></param>
            /// <param name="remoteThumbprint"></param>
            /// <typeparam name="TService"></typeparam>
            /// <returns></returns>
            public TService CreateClient<TService>(Action<ServiceEndPoint> modifyServiceEndpoint, CancellationToken cancellationToken, string? remoteThumbprint = null)
            {
                throw new Exception("Modifying the service endpoint is unsupported, since it would not actually be passed on to the remote process which holds the actual client under test.");
            }

            public TService CreateClient<TService>(Action<ServiceEndPoint> modifyServiceEndpoint)
            {
                return CreateClient<TService>(s => { }, CancellationToken.None);
            }

            public TClientService CreateClient<TService, TClientService>()
            {
                return CreateClient<TService, TClientService>(s => { });
            }

            public TClientService CreateClient<TService, TClientService>(Action<ServiceEndPoint> modifyServiceEndpoint)
            {
                throw new Exception("Unsupported, since the " + typeof(TClientService) + " would not actually be passed on to the remote process which holds the actual client under test.");
            }

            public void Dispose()
            {
                Octopus.Dispose();
                runningOldHalibutBinary.Dispose();
                tentacle.Dispose();
                portForwarder?.Dispose();
            }
        }
    }
}