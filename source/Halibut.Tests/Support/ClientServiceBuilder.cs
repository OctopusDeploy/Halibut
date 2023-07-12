#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.ServiceModel;
using Halibut.TestProxy;
using Halibut.Transport.Proxy;
using Halibut.Util;
using Octopus.TestPortForwarder;
using Serilog.Extensions.Logging;

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
        Func<HttpProxyService>? proxyFactory;
        readonly CancellationTokenSource cancellationTokenSource = new();

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

        public static ClientServiceBuilder ForServiceConnectionType(ServiceConnectionType serviceConnectionType)
        {
            switch (serviceConnectionType)
            {
                case ServiceConnectionType.Polling:
                    return Polling();
                case ServiceConnectionType.Listening:
                    return Listening();
                case ServiceConnectionType.PollingOverWebSocket:
                    return PollingOverWebSocket();
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

        public ClientServiceBuilder WithStandardServices()
        {
            return this.WithEchoService().WithMultipleParametersTestService().WithCachingService();
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

        //public ClientServiceBuilder WithProxy()
        //{
        //    this.proxyFactory = () =>
        //    {
        //        var options = new HttpProxyOptions();
        //        var loggerFactory = new SerilogLoggerFactory(new SerilogLoggerBuilder().Build());

        //        return new HttpProxyService(options, loggerFactory);
        //    };

        //    return this;
        //}

        public ClientServiceBuilder WithPendingRequestQueueFactory(Func<ILogFactory, IPendingRequestQueueFactory> pendingRequestQueueFactory)
        {
            this.pendingRequestQueueFactory = pendingRequestQueueFactory;
            return this;
        }

        public ClientServiceBuilder WithPollingReconnectRetryPolicy(Func<RetryPolicy> pollingReconnectRetryPolicy)
        {
            this.pollingReconnectRetryPolicy = pollingReconnectRetryPolicy;
            return this;
        }

        public async Task<ClientAndService> Build()
        {
            await Task.CompletedTask;
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
            var proxy = proxyFactory?.Invoke();
            ProxyDetails? proxyDetails = null;

            if (proxy != null)
            {
                await proxy.StartAsync(cancellationTokenSource.Token);
                proxyDetails = new ProxyDetails("localhost", proxy.Endpoint.Port, ProxyType.HTTP);
            }

            Uri serviceUri;

            if (serviceConnectionType == ServiceConnectionType.Polling)
            {
                var listenPort = octopus.Listen();
                portForwarder = portForwarderFactory?.Invoke(listenPort);
                serviceUri = new Uri("poll://SQ-TENTAPOLL");
                if (tentacle != null)
                {
                    if (portForwarder != null)
                    {
                        listenPort = portForwarder.ListeningPort;
                    }

                    tentacle.Poll(serviceUri, new ServiceEndPoint(new Uri("https://localhost:" + listenPort), clientCertAndThumbprint.Thumbprint, proxyDetails));
                }
            }
            else if (serviceConnectionType == ServiceConnectionType.PollingOverWebSocket)
            {
                var webSocketListeningPort = TcpPortHelper.FindFreeTcpPort();
                var webSocketPath = Guid.NewGuid().ToString();
                var webSocketListeningUrl = $"https://+:{webSocketListeningPort}/{webSocketPath}";
                var webSocketSslCertificateBindingAddress = $"0.0.0.0:{webSocketListeningPort}";

                octopus.ListenWebSocket(webSocketListeningUrl);

                var webSocketSslCertificate = new WebSocketSslCertificateBuilder(webSocketSslCertificateBindingAddress).Build();
                disposableCollection.Add(webSocketSslCertificate);

                serviceUri = new Uri("poll://SQ-TENTAPOLL");
                if (tentacle != null)
                {
                    if (portForwarder != null)
                    {
                        webSocketListeningPort = portForwarder.ListeningPort;
                    }

                    var webSocketServiceEndpointUri = new Uri($"wss://localhost:{webSocketListeningPort}/{webSocketPath}");
                    tentacle.Poll(serviceUri, new ServiceEndPoint(webSocketServiceEndpointUri, Certificates.SslThumbprint, proxyDetails));
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

            return new ClientAndService(octopus, tentacle, serviceUri, serviceCertAndThumbprint, portForwarder, disposableCollection, proxy, proxyDetails, cancellationTokenSource);
        }

        public class ClientAndService : IClientAndService
        {
            public Uri ServiceUri { get; }
            readonly CertAndThumbprint serviceCertAndThumbprint; // for creating a client
            readonly DisposableCollection disposableCollection;
            readonly ProxyDetails? proxyDetails;
            readonly CancellationTokenSource cancellationTokenSource;

            public ClientAndService(HalibutRuntime octopus,
                HalibutRuntime tentacle,
                Uri serviceUri,
                CertAndThumbprint serviceCertAndThumbprint,
                PortForwarder? portForwarder,
                DisposableCollection disposableCollection,
                HttpProxyService? proxy,
                ProxyDetails? proxyDetails,
                CancellationTokenSource cancellationTokenSource)
            {
                Octopus = octopus;
                Tentacle = tentacle;
                ServiceUri = serviceUri;
                this.serviceCertAndThumbprint = serviceCertAndThumbprint;
                PortForwarder = portForwarder;
                Proxy = proxy;
                this.disposableCollection = disposableCollection;
                this.proxyDetails = proxyDetails;
                this.cancellationTokenSource = cancellationTokenSource;
            }

            public HalibutRuntime Octopus { get; }
            public HalibutRuntime? Tentacle { get; }
            public PortForwarder? PortForwarder { get; }
            public HttpProxyService? Proxy { get; }

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
                var serviceEndpoint = new ServiceEndPoint(ServiceUri, remoteThumbprint ?? serviceCertAndThumbprint.Thumbprint, proxyDetails);
                modifyServiceEndpoint(serviceEndpoint);
                return Octopus.CreateClient<TService>(serviceEndpoint, cancellationToken);
            }

            public TClientService CreateClient<TService, TClientService>()
            {
                return CreateClient<TService, TClientService>(_ => { });
            }

            public TClientService CreateClient<TService, TClientService>(Action<ServiceEndPoint> modifyServiceEndpoint)
            {
                var serviceEndpoint = new ServiceEndPoint(ServiceUri, serviceCertAndThumbprint.Thumbprint, proxyDetails);
                modifyServiceEndpoint(serviceEndpoint);
                return Octopus.CreateClient<TService, TClientService>(serviceEndpoint);
            }

            public void Dispose()
            {
                cancellationTokenSource?.Cancel();
                cancellationTokenSource?.Dispose();
                Octopus.Dispose();
                Tentacle?.Dispose();
                Proxy?.Dispose();
                PortForwarder?.Dispose();
                disposableCollection.Dispose();
            }
        }
    }
}
