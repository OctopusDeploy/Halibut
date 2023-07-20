#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.Logging;
using Halibut.ServiceModel;
using Halibut.TestProxy;
using Halibut.TestUtils.Contracts;
using Halibut.TestUtils.Contracts.Tentacle.Services;
using Halibut.Transport.Proxy;
using Halibut.Util;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Capabilities;
using Octopus.Tentacle.Contracts.ScriptServiceV2;
using Octopus.TestPortForwarder;
using Serilog.Extensions.Logging;

namespace Halibut.Tests.Support
{
    public class LatestClientAndLatestServiceBuilder : IClientAndServiceBuilder
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
        LogLevel halibutLogLevel = LogLevel.Trace;

        public LatestClientAndLatestServiceBuilder(ServiceConnectionType serviceConnectionType, CertAndThumbprint serviceCertAndThumbprint)
        {
            this.serviceConnectionType = serviceConnectionType;
            this.serviceCertAndThumbprint = serviceCertAndThumbprint;
        }

        public static LatestClientAndLatestServiceBuilder Polling()
        {
            return new LatestClientAndLatestServiceBuilder(ServiceConnectionType.Polling, CertAndThumbprint.TentaclePolling);
        }

        public static LatestClientAndLatestServiceBuilder PollingOverWebSocket()
        {
            return new LatestClientAndLatestServiceBuilder(ServiceConnectionType.PollingOverWebSocket, CertAndThumbprint.TentaclePolling);
        }

        public static LatestClientAndLatestServiceBuilder Listening()
        {
            return new LatestClientAndLatestServiceBuilder(ServiceConnectionType.Listening, CertAndThumbprint.TentacleListening);
        }

        public static LatestClientAndLatestServiceBuilder ForServiceConnectionType(ServiceConnectionType serviceConnectionType)
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
        public LatestClientAndLatestServiceBuilder NoService()
        {
            hasService = false;
            return this;
        }

        public LatestClientAndLatestServiceBuilder WithServiceFactory(IServiceFactory serviceFactory)
        {
            this.serviceFactory = serviceFactory;
            return this;
        }
        
        public LatestClientAndLatestServiceBuilder WithService<TContract>(Func<TContract> implementation)
        {
            if (serviceFactory == null) serviceFactory = new DelegateServiceFactory();
            if (serviceFactory is not DelegateServiceFactory) throw new Exception("WithService can only be used with a delegate service factory");
            (serviceFactory as DelegateServiceFactory)?.Register(implementation);

            return this;
        }

        public LatestClientAndLatestServiceBuilder WithPortForwarding()
        {
            return WithPortForwarding(port => PortForwarderUtil.ForwardingToLocalPort(port).Build());
        }

        IClientAndServiceBuilder IClientAndServiceBuilder.WithPortForwarding(Func<int, PortForwarder> portForwarderFactory)
        {
            return this.WithPortForwarding(portForwarderFactory);
        }

        public LatestClientAndLatestServiceBuilder WithPortForwarding(Func<int, PortForwarder> portForwarderFactory)
        {
            if (this.portForwarderFactory != null)
            {
                throw new NotSupportedException("A PortForwarderFactory is already registered with the Builder. Only one PortForwarder is supported");
            }

            this.portForwarderFactory = portForwarderFactory;
            return this;
        }

        public LatestClientAndLatestServiceBuilder WithPortForwarding(out Reference<PortForwarder> portForwarder)
        {
            this.WithPortForwarding();

            this.portForwarderReference = new Reference<PortForwarder>();
            portForwarder = this.portForwarderReference;

            return this;
        }

        IClientAndServiceBuilder IClientAndServiceBuilder.WithStandardServices()
        {
            return WithStandardServices();
        }

        IClientAndServiceBuilder IClientAndServiceBuilder.WithTentacleServices()
        {
            return WithTentacleServices();
        }

        public LatestClientAndLatestServiceBuilder WithStandardServices()
        {
            return this
                .WithEchoService()
                .WithMultipleParametersTestService()
                .WithCachingService()
                .WithComplexObjectService();
        }

        public LatestClientAndLatestServiceBuilder WithTentacleServices()
        {
            return this
                .WithService<IFileTransferService>(() => new FileTransferService())
                .WithService<IScriptService>(() => new ScriptService())
                .WithService<IScriptServiceV2>(() => new ScriptServiceV2())
                .WithService<ICapabilitiesServiceV2>(() => new CapabilitiesServiceV2());
        }

        IClientAndServiceBuilder IClientAndServiceBuilder.WithCachingService() => WithCachingService();
        
        public LatestClientAndLatestServiceBuilder WithCachingService()
        {
            return this.WithService<ICachingService>(() => new CachingService());
        }

        IClientAndServiceBuilder IClientAndServiceBuilder.WithProxy()
        {
            return WithProxy();
        }

        public LatestClientAndLatestServiceBuilder WithProxy()
        {
            this.proxyFactory = () =>
            {
                var options = new HttpProxyOptions();
                var loggerFactory = new SerilogLoggerFactory(new SerilogLoggerBuilder().Build());

                return new HttpProxyService(options, loggerFactory);
            };

            return this;
        }

        public LatestClientAndLatestServiceBuilder WithPendingRequestQueueFactory(Func<ILogFactory, IPendingRequestQueueFactory> pendingRequestQueueFactory)
        {
            this.pendingRequestQueueFactory = pendingRequestQueueFactory;
            return this;
        }

        public LatestClientAndLatestServiceBuilder WithPollingReconnectRetryPolicy(Func<RetryPolicy> pollingReconnectRetryPolicy)
        {
            this.pollingReconnectRetryPolicy = pollingReconnectRetryPolicy;
            return this;
        }

        IClientAndServiceBuilder IClientAndServiceBuilder.WithHalibutLoggingLevel(LogLevel halibutLogLevel)
        {
            return WithHalibutLoggingLevel(halibutLogLevel);
        }
        
        public LatestClientAndLatestServiceBuilder WithHalibutLoggingLevel(LogLevel halibutLogLevel)
        {
            this.halibutLogLevel = halibutLogLevel;

            return this;
        }
        
        async Task<IClientAndService> IClientAndServiceBuilder.Build(CancellationToken cancellationToken)
        {
            return await Build(cancellationToken);
        }

        public async Task<ClientAndService> Build(CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            CancellationTokenSource cancellationTokenSource = new();
            
            serviceFactory ??= new DelegateServiceFactory();

            var octopusLogFactory = new TestContextLogFactory("Client", halibutLogLevel);
            var octopusBuilder = new HalibutRuntimeBuilder()
                .WithServerCertificate(clientCertAndThumbprint.Certificate2)
                .WithLogFactory(octopusLogFactory);

            if (pendingRequestQueueFactory != null)
            {
                octopusBuilder = octopusBuilder.WithPendingRequestQueueFactory(pendingRequestQueueFactory(octopusLogFactory));
            }

            var client = octopusBuilder.Build();
            client.Trust(serviceCertAndThumbprint.Thumbprint);

            HalibutRuntime? service = null;
            if (hasService)
            {
                var serviceBuilder = new HalibutRuntimeBuilder()
                    .WithServiceFactory(serviceFactory)
                    .WithServerCertificate(serviceCertAndThumbprint.Certificate2)
                    .WithLogFactory(new TestContextLogFactory("Service", halibutLogLevel));

                if(pollingReconnectRetryPolicy != null) serviceBuilder.WithPollingReconnectRetryPolicy(pollingReconnectRetryPolicy);
                service = serviceBuilder.Build();
            }

            var disposableCollection = new DisposableCollection();
            PortForwarder? portForwarder = null;
            var httpProxy = proxyFactory?.Invoke();
            ProxyDetails? httpProxyDetails = null;

            if (httpProxy != null)
            {
                await httpProxy.StartAsync(cancellationTokenSource.Token);
                httpProxyDetails = new ProxyDetails("localhost", httpProxy.Endpoint.Port, ProxyType.HTTP);
            }

            Uri serviceUri;

            if (serviceConnectionType == ServiceConnectionType.Polling)
            {
                var clientListenPort = client.Listen();
                portForwarder = portForwarderFactory?.Invoke(clientListenPort);
                serviceUri = new Uri("poll://SQ-TENTAPOLL");
                if (service != null)
                {
                    if (portForwarder != null)
                    {
                        clientListenPort = portForwarder.ListeningPort;
                    }

                    service.Poll(serviceUri, new ServiceEndPoint(new Uri("https://localhost:" + clientListenPort), clientCertAndThumbprint.Thumbprint, httpProxyDetails));
                }
            }
            else if (serviceConnectionType == ServiceConnectionType.PollingOverWebSocket)
            {
                var webSocketListeningPort = TcpPortHelper.FindFreeTcpPort();
                var webSocketPath = Guid.NewGuid().ToString();
                var webSocketListeningUrl = $"https://+:{webSocketListeningPort}/{webSocketPath}";
                var webSocketSslCertificateBindingAddress = $"0.0.0.0:{webSocketListeningPort}";

                client.ListenWebSocket(webSocketListeningUrl);

                var webSocketSslCertificate = new WebSocketSslCertificateBuilder(webSocketSslCertificateBindingAddress).Build();
                disposableCollection.Add(webSocketSslCertificate);

                serviceUri = new Uri("poll://SQ-TENTAPOLL");
                if (service != null)
                {
                    portForwarder = portForwarderFactory?.Invoke(webSocketListeningPort);

                    if (portForwarder != null)
                    {
                        webSocketListeningPort = portForwarder.ListeningPort;
                    }

                    var webSocketServiceEndpointUri = new Uri($"wss://localhost:{webSocketListeningPort}/{webSocketPath}");
                    service.Poll(serviceUri, new ServiceEndPoint(webSocketServiceEndpointUri, Certificates.SslThumbprint, httpProxyDetails));
                }
            }
            else if (serviceConnectionType == ServiceConnectionType.Listening)
            {
                int listenPort;
                if (service != null)
                {
                    service.Trust(clientCertAndThumbprint.Thumbprint);
                    listenPort = service.Listen();
                }
                else
                {
                    var dummyTentacle = new TCPListenerWhichKillsNewConnections();
                    disposableCollection.Add(dummyTentacle);
                    listenPort = dummyTentacle.Port;
                }

                portForwarder = portForwarderFactory?.Invoke(listenPort);
                if (portForwarder != null)
                {
                    listenPort = portForwarder.ListeningPort;
                }

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

            return new ClientAndService(client, service, serviceUri, serviceCertAndThumbprint, portForwarder, disposableCollection, httpProxy, httpProxyDetails, cancellationTokenSource);
        }

        public class ClientAndService : IClientAndService
        {
            public Uri ServiceUri { get; }
            readonly CertAndThumbprint serviceCertAndThumbprint; // for creating a client
            readonly DisposableCollection disposableCollection;
            readonly ProxyDetails? proxyDetails;
            readonly CancellationTokenSource cancellationTokenSource;

            public ClientAndService(HalibutRuntime client,
                HalibutRuntime service,
                Uri serviceUri,
                CertAndThumbprint serviceCertAndThumbprint,
                PortForwarder? portForwarder,
                DisposableCollection disposableCollection,
                HttpProxyService? proxy,
                ProxyDetails? proxyDetails,
                CancellationTokenSource cancellationTokenSource)
            {
                Client = client;
                Service = service;
                ServiceUri = serviceUri;
                this.serviceCertAndThumbprint = serviceCertAndThumbprint;
                PortForwarder = portForwarder;
                HttpProxy = proxy;
                this.disposableCollection = disposableCollection;
                this.proxyDetails = proxyDetails;
                this.cancellationTokenSource = cancellationTokenSource;
            }

            public HalibutRuntime Client { get; }
            public HalibutRuntime? Service { get; }
            public PortForwarder? PortForwarder { get; }
            public HttpProxyService? HttpProxy { get; }

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
                return Client.CreateClient<TService>(serviceEndpoint, cancellationToken);
            }

            public TClientService CreateClient<TService, TClientService>()
            {
                return CreateClient<TService, TClientService>(_ => { });
            }

            public TClientService CreateClient<TService, TClientService>(Action<ServiceEndPoint> modifyServiceEndpoint)
            {
                var serviceEndpoint = new ServiceEndPoint(ServiceUri, serviceCertAndThumbprint.Thumbprint, proxyDetails);
                modifyServiceEndpoint(serviceEndpoint);
                return Client.CreateClient<TService, TClientService>(serviceEndpoint);
            }

            public void Dispose()
            {
                var logger = new SerilogLoggerBuilder().Build().ForContext<ClientAndService>();
                logger.Information("Dispose called");
                Action<Exception> logError = e => logger.Warning(e, "Ignoring error in dispose");

                Try.CatchingError(() => cancellationTokenSource?.Cancel(), logError);
                Try.CatchingError(Client.Dispose, logError);
                Try.CatchingError(() => Service?.Dispose(), logError);
                Try.CatchingError(() => HttpProxy?.Dispose(), logError);
                Try.CatchingError(() => PortForwarder?.Dispose(), logError);
                Try.CatchingError(disposableCollection.Dispose, logError);
                Try.CatchingError(() => cancellationTokenSource?.Dispose(), logError);
            }
        }
    }
}
