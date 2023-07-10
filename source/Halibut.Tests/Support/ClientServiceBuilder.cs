#nullable enable
using System;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using Halibut.Diagnostics;
using Halibut.ServiceModel;
using Halibut.Util;
using Octopus.TestPortForwarder;

namespace Halibut.Tests.Support
{
    public class ClientServiceBuilder
    {
        IServiceFactory? serviceFactory;
        readonly ServiceConnectionType serviceConnectionType;
        readonly CertAndThumbprint serviceCertAndThumbprint;
        readonly CertAndThumbprint clientCertAndThumbprint = CertAndThumbprint.Octopus;
        bool hasService = true;
        Func<int, PortForwarder>? portForwarderFactory;
        Func<ILogFactory, IPendingRequestQueueFactory>? pendingRequestQueueFactory;
        Reference<PortForwarder>? portForwarderReference;
        Func<RetryPolicy>? pollingReconnectRetryPolicy;

        public ClientServiceBuilder(ServiceConnectionType serviceConnectionType, CertAndThumbprint serviceCertAndThumbprint)
        {
            this.serviceConnectionType = serviceConnectionType;
            this.serviceCertAndThumbprint = serviceCertAndThumbprint;
        }

        public static ClientServiceBuilder Polling()
        {
            return new ClientServiceBuilder(ServiceConnectionType.Polling, CertAndThumbprint.TentaclePolling);
        }

        public static ClientServiceBuilder PollingOverWebSocket()
        {
            return new ClientServiceBuilder(ServiceConnectionType.PollingOverWebSocket, CertAndThumbprint.TentaclePolling);
        }

        public static ClientServiceBuilder Listening()
        {
            return new ClientServiceBuilder(ServiceConnectionType.Listening, CertAndThumbprint.TentacleListening);
        }

        public static ClientServiceBuilder ForMode(ServiceConnectionType serviceConnectionType)
        {
            switch (serviceConnectionType)
            {
                case ServiceConnectionType.Polling:
                    return Polling();
                case ServiceConnectionType.Listening:
                    return Listening();
                default:
                    throw new ArgumentOutOfRangeException(nameof(serviceConnectionType), serviceConnectionType, null);
            }
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

        public ClientServiceBuilder WithService<TContract>(Func<TContract> implementation)
        {
            if (serviceFactory == null) serviceFactory = new DelegateServiceFactory();
            if (serviceFactory is not DelegateServiceFactory) throw new Exception("WithService can only be used with a delegate service factory");
            (serviceFactory as DelegateServiceFactory)?.Register(implementation);

            return this;
        }

        public ClientServiceBuilder WithPortForwarding()
        {
            return WithPortForwarding(port => PortForwarderUtil.ForwardingToLocalPort(port).Build());
        }

        public ClientServiceBuilder WithPortForwarding(out Reference<PortForwarder> portForwarder)
        {
            this.WithPortForwarding();

            this.portForwarderReference = new Reference<PortForwarder>();
            portForwarder = this.portForwarderReference;

            return this;
        }

        public ClientServiceBuilder WithPortForwarding(Func<int, PortForwarder> portForwarderFactory)
        {
            this.portForwarderFactory = portForwarderFactory;
            return this;
        }

        public ClientServiceBuilder WithPendingRequestQueueFactory(Func<ILogFactory, IPendingRequestQueueFactory> pendingRequestQueueFactory)
        {
            this.pendingRequestQueueFactory = pendingRequestQueueFactory;
            return this;
        }

        public ClientAndService Build()
        {
            serviceFactory ??= new DelegateServiceFactory();

            var octopusLogFactory = new TestContextLogFactory("Client");
            var octopusBuilder = new HalibutRuntimeBuilder()
                .WithServerCertificate(clientCertAndThumbprint.Certificate2)
                .WithLogFactory(octopusLogFactory);

            if (pendingRequestQueueFactory != null)
            {
                octopusBuilder = octopusBuilder.WithPendingRequestQueueFactory(pendingRequestQueueFactory(octopusLogFactory));
            }

            var octopus = octopusBuilder.Build();
            octopus.Trust(serviceCertAndThumbprint.Thumbprint);

            HalibutRuntime? tentacle = null;
            if (hasService)
            {
                var tentacleBuilder = new HalibutRuntimeBuilder()
                    .WithServiceFactory(serviceFactory)
                    .WithServerCertificate(serviceCertAndThumbprint.Certificate2)
                    .WithLogFactory(new TestContextLogFactory("Service"));

                if(pollingReconnectRetryPolicy != null) tentacleBuilder.WithPollingReconnectRetryPolicy(pollingReconnectRetryPolicy);
                tentacle = tentacleBuilder.Build();
            }

            var disposableCollection = new DisposableCollection();
            PortForwarder? portForwarder = null;
            Uri serviceUri;

            if (serviceConnectionType == ServiceConnectionType.Polling)
            {
                var listenPort = octopus.Listen();
                portForwarder = portForwarderFactory?.Invoke(listenPort);
                serviceUri = new Uri("poll://SQ-TENTAPOLL");
                if (tentacle != null)
                {
                    if (portForwarder != null) listenPort = portForwarder.ListeningPort;
                    tentacle.Poll(serviceUri, new ServiceEndPoint(new Uri("https://localhost:" + listenPort), clientCertAndThumbprint.Thumbprint));
                }
            }
            else if (serviceConnectionType == ServiceConnectionType.PollingOverWebSocket)
            {
                static int FindFreeTcpPort()
                {
                    var listener = new TcpListener(IPAddress.Loopback, 0);
                    listener.Start();
                    var port = ((IPEndPoint)listener.LocalEndpoint).Port;
                    listener.Stop();
                    return port;
                }

                var webSocketListeningPort = FindFreeTcpPort();
                var webSocketPath = Guid.NewGuid().ToString();
                var webSocketListeningUrl = $"https://+:{webSocketListeningPort}/{webSocketPath}";
                var webSocketServiceEndpointUri = new Uri($"wss://localhost:{webSocketListeningPort}/{webSocketPath}");
                var webSocketSslCertificateBindingAddress = $"0.0.0.0:{webSocketListeningPort}";

                octopus.ListenWebSocket(webSocketListeningUrl);

                var webSocketSslCertificate = new WebSocketSslCertificateBuilder(webSocketSslCertificateBindingAddress).Build();
                disposableCollection.Add(webSocketSslCertificate);

                if (portForwarderFactory != null)
                {
                    throw new NotSupportedException("The PortForwarder is not currently supported with WebSockets");
                }

                serviceUri = new Uri("poll://SQ-TENTAPOLL");
                if (tentacle != null)
                {
                    tentacle.Poll(serviceUri, new ServiceEndPoint(webSocketServiceEndpointUri, Certificates.SslThumbprint));
                }
            }
            else if (serviceConnectionType == ServiceConnectionType.Listening)
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
            else
            {
                throw new NotSupportedException();
            }

            if (portForwarderReference != null && portForwarder != null)
            {
                portForwarderReference.Value = portForwarder;
            }

            return new ClientAndService(octopus, tentacle, serviceUri, serviceCertAndThumbprint, portForwarder, disposableCollection);
        }

        public class ClientAndService : IClientAndService
        {
            public HalibutRuntime Octopus { get; }
            public HalibutRuntime? Tentacle { get; }
            public PortForwarder? PortForwarder { get; }
            public Uri ServiceUri { get; }
            readonly CertAndThumbprint serviceCertAndThumbprint; // for creating a client
            readonly DisposableCollection disposableCollection;

            public ClientAndService(HalibutRuntime octopus,
                HalibutRuntime tentacle,
                Uri serviceUri,
                CertAndThumbprint serviceCertAndThumbprint,
                PortForwarder? portForwarder,
                DisposableCollection disposableCollection)
            {
                Octopus = octopus;
                Tentacle = tentacle;
                ServiceUri = serviceUri;
                this.serviceCertAndThumbprint = serviceCertAndThumbprint;
                PortForwarder = portForwarder;
                this.disposableCollection = disposableCollection;
            }

            public TService CreateClient<TService>(CancellationToken? cancellationToken = null, string? remoteThumbprint = null)
            {
                return CreateClient<TService>(_ => { }, cancellationToken ?? CancellationToken.None, remoteThumbprint);
            }

            public TService CreateClient<TService>(Action<ServiceEndPoint> modifyServiceEndpoint)
            {
                return CreateClient<TService>(modifyServiceEndpoint, CancellationToken.None);
            }

            public TService CreateClient<TService>(Action<ServiceEndPoint> modifyServiceEndpoint, CancellationToken cancellationToken, string? remoteThumbprint = null)
            {
                var serviceEndpoint = new ServiceEndPoint(ServiceUri, remoteThumbprint ?? serviceCertAndThumbprint.Thumbprint);
                modifyServiceEndpoint(serviceEndpoint);
                return Octopus.CreateClient<TService>(serviceEndpoint, cancellationToken);
            }

            public TClientService CreateClient<TService, TClientService>()
            {
                return CreateClient<TService, TClientService>(_ => { });
            }

            public TClientService CreateClient<TService, TClientService>(Action<ServiceEndPoint> modifyServiceEndpoint)
            {
                var serviceEndpoint = new ServiceEndPoint(ServiceUri, serviceCertAndThumbprint.Thumbprint);
                modifyServiceEndpoint(serviceEndpoint);
                return Octopus.CreateClient<TService, TClientService>(serviceEndpoint);
            }

            public void Dispose()
            {
                Octopus.Dispose();
                Tentacle?.Dispose();
                PortForwarder?.Dispose();
                disposableCollection.Dispose();
            }
        }

        class WebSocketSslCertificateBuilder
        {
            readonly string bindingAddress;

            public WebSocketSslCertificateBuilder(string bindingAddress)
            {
                this.bindingAddress = bindingAddress;
            }

            public WebSocketSslCertificate Build()
            {
                WebSocketSslCertificateHelper.AddSslCertToLocalStoreAndRegisterFor(bindingAddress);

                var webSocketSslCertificate = new WebSocketSslCertificate(bindingAddress);

                return webSocketSslCertificate;
            }
        }

        class WebSocketSslCertificate : IDisposable
        {
            readonly string bindingAddress;

            internal WebSocketSslCertificate(string bindingAddress)
            {
                this.bindingAddress = bindingAddress;
            }

            public void Dispose()
            {
                WebSocketSslCertificateHelper.RemoveSslCertBindingFor(bindingAddress);
            }
        }

        public ClientServiceBuilder WithPollingReconnectRetryPolicy(Func<RetryPolicy> pollingReconnectRetryPolicy)
        {
            this.pollingReconnectRetryPolicy = pollingReconnectRetryPolicy;
            return this;
        }
    }
}