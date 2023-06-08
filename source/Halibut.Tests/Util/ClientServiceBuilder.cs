#nullable enable
using System;
using System.CodeDom;
using System.Threading;
using FluentAssertions.Primitives;
using Halibut.ServiceModel;
using Halibut.Tests.Util.TcpUtils;

namespace Halibut.Tests.Util
{
    public class ClientServiceBuilder
    {
        IServiceFactory? serviceFactory;
        readonly ServiceConnectionType serviceConnectionType;
        readonly CertAndThumbprint serviceCertAndThumbprint;
        readonly CertAndThumbprint clientCertAndThumbprint = CertAndThumbprint.Octopus;
        bool hasService = true;

        Func<int, PortForwarder>? portForwarderFactory;
        HalibutRuntime? existingOctopus;
        int? existingPort;

        public ClientServiceBuilder(ServiceConnectionType serviceConnectionType, CertAndThumbprint serviceCertAndThumbprint)
        {
            this.serviceConnectionType = serviceConnectionType;
            this.serviceCertAndThumbprint = serviceCertAndThumbprint;
        }

        public static ClientServiceBuilder Polling()
        {
            return new ClientServiceBuilder(ServiceConnectionType.Polling, CertAndThumbprint.TentaclePolling);
        }

        public static ClientServiceBuilder Listening()
        {
            return new ClientServiceBuilder(ServiceConnectionType.Listening, CertAndThumbprint.TentacleListening);
        }

        public static ClientServiceBuilder ForServiceConnectionType(ServiceConnectionType serviceConnectionType)
        {
            return new ClientServiceBuilder(serviceConnectionType, CertAndThumbprint.TentacleListening);
        }

        /// <summary>
        ///     Ie no tentacle.
        ///     In the case of listening, a TCPListenerWhichKillsNewConnections will be created. This will cause connections to
        ///     that port to be killed immediately.
        /// </summary>
        /// <returns></returns>
        public ClientServiceBuilder NoService()
        {
            hasService = false;
            return this;
        }

        public ClientServiceBuilder WithServiceFactory(IServiceFactory serviceFactory)
        {
            this.serviceFactory = serviceFactory;
            return this;
        }

        public ClientServiceBuilder WithService<TContract>(TContract implementation)
        {
            return WithService(() => implementation);
        }

        public ClientServiceBuilder WithService<TContract>(Func<TContract> implementation)
        {
            if (serviceFactory == null) serviceFactory = new DelegateServiceFactory();
            if (serviceFactory is not DelegateServiceFactory) throw new Exception("WithService can only be used with a delegate service factory");
            (serviceFactory as DelegateServiceFactory)?.Register(implementation);

            return this;
        }

        public ClientServiceBuilder WithPortForwarding()
        {
            return WithPortForwarding(port => PortForwarderBuilder.ForwardingToLocalPort(port).Build());
        }
        public ClientServiceBuilder WithPortForwarding(Func<int, PortForwarder> portForwarderFactory)
        {
            this.portForwarderFactory = portForwarderFactory;
            return this;
        }

        public ClientServiceBuilder WithExistingOctopus(HalibutRuntime octopus, int? port)
        {
            this.existingOctopus = octopus;
            this.existingPort = port;
            return this;
        }

        public IClientAndService Build()
        {
            serviceFactory ??= new DelegateServiceFactory();

            var useExistingOctopus = this.existingOctopus != null;

            if (useExistingOctopus && portForwarderFactory != null)
            {
                throw new NotSupportedException("An existing HalibutRuntime and PortForwarding cannot be used together");
            }

            var octopus = this.existingOctopus ?? new HalibutRuntime(clientCertAndThumbprint.Certificate2);

            if (!useExistingOctopus)
            {
                octopus.Trust(serviceCertAndThumbprint.Thumbprint);
            }

            HalibutRuntime? tentacle = null;
            if (hasService)
            {
                tentacle = new HalibutRuntime(serviceFactory, serviceCertAndThumbprint.Certificate2);
            }

            var disposableCollection = new DisposableCollection();

            PortForwarder? portForwarder = null;
            Uri serviceUri;
            if (serviceConnectionType == ServiceConnectionType.Polling)
            {
                var listenPort = useExistingOctopus ? this.existingPort!.Value : octopus.Listen();
                portForwarder = portForwarderFactory != null ? portForwarderFactory(listenPort) : null;
                serviceUri = new Uri("poll://SQ-TENTAPOLL");
                if (tentacle != null)
                {
                    if (portForwarder != null) listenPort = portForwarder.ListeningPort;
                    tentacle.Poll(serviceUri, new ServiceEndPoint(new Uri("https://localhost:" + listenPort), clientCertAndThumbprint.Thumbprint));
                }
            }
            else
            {
                int listenPort;
                if (tentacle != null)
                {
                    tentacle.Trust(clientCertAndThumbprint.Thumbprint);
                    listenPort = tentacle.Listen();
                }
                else
                {
                    var dummyTentacle = new TCPListenerWhichKillsNewConnections();
                    disposableCollection.Add(dummyTentacle);
                    listenPort = dummyTentacle.Port;
                }

                portForwarder = portForwarderFactory != null ? portForwarderFactory(listenPort) : null;
                if (portForwarder != null) listenPort = portForwarder.ListeningPort;
                serviceUri = new Uri("https://localhost:" + listenPort);
            }

            return new ClientAndService(octopus, tentacle, serviceUri, serviceCertAndThumbprint, portForwarder, disposableCollection);
        }

        public class ClientAndService : IClientAndService
        {
            readonly HalibutRuntime octopus;
            readonly HalibutRuntime? tentacle;
            readonly PortForwarder? portForwarder;
            readonly Uri serviceUri;
            readonly CertAndThumbprint serviceCertAndThumbprint; // for creating a client
            readonly DisposableCollection disposableCollection;

            public ClientAndService(HalibutRuntime octopus,
                HalibutRuntime tentacle,
                Uri serviceUri,
                CertAndThumbprint serviceCertAndThumbprint,
                PortForwarder? portForwarder,
                DisposableCollection disposableCollection)
            {
                this.octopus = octopus;
                this.tentacle = tentacle;
                this.serviceUri = serviceUri;
                this.serviceCertAndThumbprint = serviceCertAndThumbprint;
                this.portForwarder = portForwarder;
                this.disposableCollection = disposableCollection;
            }

            public IHalibutRuntime Octopus => octopus;
            public PortForwarder PortForwarder => portForwarder;

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

            public TClientService CreateClient<TService, TClientService>()
            {
                return CreateClient<TService, TClientService>(_ => { });
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
                tentacle?.Dispose();
                portForwarder?.Dispose();
                disposableCollection.Dispose();
            }
        }
    }
}