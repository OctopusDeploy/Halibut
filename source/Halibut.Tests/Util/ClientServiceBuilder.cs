using System;
using System.Threading;
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
        bool HasService = true;

        Func<int, IPortForwarder> portForwarderFactory = port => new NullPortForwarder(port);

        public ClientServiceBuilder(ServiceConnectionType serviceConnectionType, CertAndThumbprint serviceCertAndThumbprint)
        {
            this.serviceConnectionType = serviceConnectionType;
            this.serviceCertAndThumbprint = serviceCertAndThumbprint;
        }

        public static ClientServiceBuilder Polling()
        {
            new ClientServiceBuilder(ServiceConnectionType.Polling, CertAndThumbprint.TentaclePolling);
            return new ClientServiceBuilder(ServiceConnectionType.Polling, CertAndThumbprint.TentaclePolling);
        }

        public static ClientServiceBuilder Listening()
        {
            return new ClientServiceBuilder(ServiceConnectionType.Listening, CertAndThumbprint.TentacleListening);
        }

        /// <summary>
        ///     Ie no tentacle.
        ///     In the case of listening, a TCPListenerWhichKillsNewConnections will be created. This will cause connections to
        ///     that port to be killed immediately.
        /// </summary>
        /// <returns></returns>
        public ClientServiceBuilder NoService()
        {
            HasService = false;
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
            return WithPortForwarding(port => new PortForwarder(new Uri("https://localhost:" + port), TimeSpan.Zero));
        }
        public ClientServiceBuilder WithPortForwarding(Func<int, IPortForwarder> portForwarderFactory)
        {
            this.portForwarderFactory = portForwarderFactory;
            return this;
        }

        public ClientAndService Build()
        {
            serviceFactory = serviceFactory ?? new DelegateServiceFactory();
            var octopus = new HalibutRuntime(clientCertAndThumbprint.Certificate2);
            octopus.Trust(serviceCertAndThumbprint.Thumbprint);

            HalibutRuntime? tentacle = null;
            if (HasService) tentacle = new HalibutRuntime(serviceFactory, serviceCertAndThumbprint.Certificate2);

            var disposableCollection = new DisposableCollection();

            IPortForwarder portForwarder;
            Uri serviceUri;
            CertAndThumbprint certForClientCreation;
            if (serviceConnectionType == ServiceConnectionType.Polling)
            {
                var listenPort = octopus.Listen();
                portForwarder = portForwarderFactory(listenPort);
                serviceUri = new Uri("poll://SQ-TENTAPOLL");
                if (tentacle != null) tentacle.Poll(serviceUri, new ServiceEndPoint(new Uri("https://localhost:" + portForwarder.ListeningPort), clientCertAndThumbprint.Thumbprint));
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

                portForwarder = portForwarderFactory(listenPort);
                serviceUri = new Uri("https://localhost:" + portForwarder.ListeningPort);
            }

            return new ClientAndService(octopus, tentacle, serviceUri, serviceCertAndThumbprint, portForwarder, disposableCollection);
        }

        public class ClientAndService : IDisposable
        {
            readonly HalibutRuntime octopus;
            readonly HalibutRuntime? tentacle;
            public readonly IPortForwarder portForwarder;
            readonly Uri serviceUri;
            readonly CertAndThumbprint serviceCertAndThumbprint; // for creating a client
            readonly DisposableCollection disposableCollection;

            public ClientAndService(HalibutRuntime octopus,
                HalibutRuntime tentacle,
                Uri serviceUri,
                CertAndThumbprint serviceCertAndThumbprint,
                IPortForwarder portForwarder, DisposableCollection disposableCollection)
            {
                this.octopus = octopus;
                this.tentacle = tentacle;
                this.serviceUri = serviceUri;
                this.serviceCertAndThumbprint = serviceCertAndThumbprint;
                this.portForwarder = portForwarder;
                this.disposableCollection = disposableCollection;
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

            public void Dispose()
            {
                octopus.Dispose();
                tentacle?.Dispose();
                portForwarder.Dispose();
                disposableCollection.Dispose();
            }
        }
    }
}