using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.Diagnostics.LogCreators;
using Halibut.Diagnostics.LogWriters;
using Halibut.Logging;
using Halibut.ServiceModel;
using Halibut.TestProxy;
using Halibut.Tests.Builders;
using Halibut.Tests.Support.Logging;
using Halibut.Tests.Support.TestAttributes;
using Halibut.Tests.TestServices;
using Halibut.Tests.TestServices.AsyncSyncCompat;
using Halibut.TestUtils.Contracts;
using Halibut.TestUtils.Contracts.Tentacle.Services;
using Halibut.Transport.Observability;
using Halibut.Transport.Proxy;
using Halibut.Transport.Streams;
using Halibut.Util;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Capabilities;
using Octopus.Tentacle.Contracts.ScriptServiceV2;
using Octopus.TestPortForwarder;
using Serilog.Extensions.Logging;
using ICachingService = Halibut.TestUtils.Contracts.ICachingService;
using ILog = Halibut.Diagnostics.ILog;

namespace Halibut.Tests.Support
{
    public class LatestClientAndLatestServiceBuilder : IClientAndServiceBuilder
    {
        ServiceFactoryBuilder serviceFactoryBuilder = new();
        IServiceFactory? serviceFactory;
        readonly ServiceConnectionType serviceConnectionType;
        readonly CertAndThumbprint serviceCertAndThumbprint;
        string clientTrustsThumbprint; 
        readonly CertAndThumbprint clientCertAndThumbprint;
        string serviceTrustsThumbprint;
        ForceClientProxyType? forceClientProxyType;
        AsyncHalibutFeature serviceAsyncHalibutFeature = AsyncHalibutFeature.Disabled;
        IRpcObserver? clientRpcObserver;


        bool hasService = true;
        Func<int, PortForwarder>? portForwarderFactory;
        Func<ILogFactory, IPendingRequestQueueFactory>? pendingRequestQueueFactory;
        Action<PendingRequestQueueFactoryBuilder>? pendingRequestQueueFactoryBuilder;
        Reference<PortForwarder>? portForwarderReference;
        Func<RetryPolicy>? pollingReconnectRetryPolicy;
        ProxyFactory? proxyFactory;
        LogLevel halibutLogLevel = LogLevel.Info;
        ConcurrentDictionary<string, ILog>? clientInMemoryLoggers;
        ConcurrentDictionary<string, ILog>? serviceInMemoryLoggers;
        ITrustProvider clientTrustProvider;
        Func<string, string, UnauthorizedClientConnectResponse> clientOnUnauthorizedClientConnect;
        HalibutTimeoutsAndLimits? halibutTimeoutsAndLimits;

        IStreamFactory? streamFactory;


        public LatestClientAndLatestServiceBuilder(ServiceConnectionType serviceConnectionType,
            CertAndThumbprint clientCertAndThumbprint,
            CertAndThumbprint serviceCertAndThumbprint)
        {
            this.serviceConnectionType = serviceConnectionType;
            this.serviceCertAndThumbprint = serviceCertAndThumbprint;
            this.clientCertAndThumbprint = clientCertAndThumbprint;
            clientTrustsThumbprint = serviceCertAndThumbprint.Thumbprint;
            serviceTrustsThumbprint = clientCertAndThumbprint.Thumbprint;
        }

        public static LatestClientAndLatestServiceBuilder Polling()
        {
            return new LatestClientAndLatestServiceBuilder(ServiceConnectionType.Polling, CertAndThumbprint.Octopus, CertAndThumbprint.TentaclePolling);
        }

        public static LatestClientAndLatestServiceBuilder PollingOverWebSocket()
        {
            return new LatestClientAndLatestServiceBuilder(ServiceConnectionType.PollingOverWebSocket, CertAndThumbprint.Ssl, CertAndThumbprint.TentaclePolling);
        }

        public static LatestClientAndLatestServiceBuilder Listening()
        {
            return new LatestClientAndLatestServiceBuilder(ServiceConnectionType.Listening, CertAndThumbprint.Octopus, CertAndThumbprint.TentacleListening);
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

        IClientAndServiceBuilder IClientAndServiceBuilder.NoService()
        {
            return NoService();
        }

        public LatestClientAndLatestServiceBuilder WithStreamFactory(IStreamFactory streamFactory)
        {
            this.streamFactory = streamFactory;
            return this;
        }

        public LatestClientAndLatestServiceBuilder WithHalibutTimeoutsAndLimits(HalibutTimeoutsAndLimits halibutTimeoutsAndLimits)
        {
            this.halibutTimeoutsAndLimits = halibutTimeoutsAndLimits;
            return this;
        }

        public LatestClientAndLatestServiceBuilder WithAsyncConventionsDisabled()
        {
            serviceFactoryBuilder = serviceFactoryBuilder.WithConventionVerificationDisabled();
            return this;
        }

        public LatestClientAndLatestServiceBuilder WithServiceFactory(IServiceFactory serviceFactory)
        {
            this.serviceFactory = serviceFactory;
            return this;
        }
        
        public LatestClientAndLatestServiceBuilder WithService<TContract>(Func<TContract> implementation)
        {
            serviceFactoryBuilder.WithService(implementation);
            
            if (serviceFactory != null)
            {
                if (serviceFactory is DelegateServiceFactory delegateServiceFactory)
                {
                    delegateServiceFactory.Register(implementation);
                }
                else
                {
                    throw new Exception("WithService can only be used with a custom ServiceFactory if it is a DelegateServiceFactory");
                }
            }

            return this;
        }

        public LatestClientAndLatestServiceBuilder WithAsyncService<TContract, TClientContract>(Func<TClientContract> implementation)
        {
            serviceFactoryBuilder.WithService<TContract, TClientContract>(implementation);
            
            if (serviceFactory != null)
            {
                if (serviceFactory is DelegateServiceFactory delegateServiceFactory)
                {
                    delegateServiceFactory.Register<TContract, TClientContract>(implementation);
                }
                else
                {
                    throw new Exception("WithService can only be used with a custom ServiceFactory if it is a DelegateServiceFactory");
                }
            }

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
            if(this.portForwarderFactory == null) this.WithPortForwarding();

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
                .WithComplexObjectService()
                .WithLockService()
                .WithCountingService()
                .WithReadDataStreamService();
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
            this.proxyFactory = new ProxyFactory();
            return this;
        }

        private bool ProxyDelaySendingSectionsOfHttpHeaders()
        {
            // We can only delay sending sections of the HTTP headers in versions that have the fix, which is only in async.
            return this.serviceAsyncHalibutFeature == AsyncHalibutFeature.Enabled && this.forceClientProxyType == ForceClientProxyType.AsyncClient;
        }

        public LatestClientAndLatestServiceBuilder WithPendingRequestQueueFactory(Func<ILogFactory, IPendingRequestQueueFactory> pendingRequestQueueFactory)
        {
            this.pendingRequestQueueFactory = pendingRequestQueueFactory;
            return this;
        }

        public LatestClientAndLatestServiceBuilder WithPendingRequestQueueFactoryBuilder(Action<PendingRequestQueueFactoryBuilder> pendingRequestQueueFactoryBuilder)
        {
            this.pendingRequestQueueFactoryBuilder = pendingRequestQueueFactoryBuilder;
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
        
        public LatestClientAndLatestServiceBuilder RecordingClientLogs(out ConcurrentDictionary<string, ILog> inMemoryLoggers)
        {
            inMemoryLoggers = new ConcurrentDictionary<string, ILog>();
            this.clientInMemoryLoggers = inMemoryLoggers;
            return this;
        }
        
        public LatestClientAndLatestServiceBuilder RecordingServiceLogs(out ConcurrentDictionary<string, ILog> inMemoryLoggers)
        {
            inMemoryLoggers = new ConcurrentDictionary<string, ILog>(); 
            this.serviceInMemoryLoggers = inMemoryLoggers;
            return this;
        }

        public LatestClientAndLatestServiceBuilder WithClientOnUnauthorizedClientConnect(Func<string, string, UnauthorizedClientConnectResponse> onUnauthorizedClientConnect)
        {
            clientOnUnauthorizedClientConnect = onUnauthorizedClientConnect;
            return this;
        }
        
        public LatestClientAndLatestServiceBuilder WithClientTrustProvider(ITrustProvider trustProvider)
        {
            clientTrustProvider = trustProvider;
            return this;
        }

        public LatestClientAndLatestServiceBuilder WithClientTrustingTheWrongCertificate()
        {
            clientTrustsThumbprint = CertAndThumbprint.Wrong.Thumbprint;
            return this;
        }
        
        public LatestClientAndLatestServiceBuilder WithServiceTrustingTheWrongCertificate()
        {
            serviceTrustsThumbprint = CertAndThumbprint.Wrong.Thumbprint;
            return this;
        }

        IClientAndServiceBuilder IClientAndServiceBuilder.WithForcingClientProxyType(ForceClientProxyType forceClientProxyType)
        {
            return WithForcingClientProxyType(forceClientProxyType);
        }

        public IClientAndServiceBuilder WithServiceAsyncHalibutFeatureEnabled()
        {
            serviceAsyncHalibutFeature = AsyncHalibutFeature.Enabled;
            return this;
        }

        public LatestClientAndLatestServiceBuilder WithForcingClientProxyType(ForceClientProxyType forceClientProxyType)
        {
            this.forceClientProxyType = forceClientProxyType;
            return this;
        }
        
        public LatestClientAndLatestServiceBuilder WithClientRpcObserver(IRpcObserver? clientRpcObserver)
        {
            this.clientRpcObserver = clientRpcObserver;
            return this;
        }

        async Task<IClientAndService> IClientAndServiceBuilder.Build(CancellationToken cancellationToken)
        {
            return await Build(cancellationToken);
        }

        public async Task<ClientAndService> Build(CancellationToken cancellationToken)
        {
            var logger = new SerilogLoggerBuilder().Build().ForContext<LatestClientAndLatestServiceBuilder>();
            CancellationTokenSource cancellationTokenSource = new();
            
            serviceFactory ??= serviceFactoryBuilder.Build();
            var octopusLogFactory = BuildClientLogger();

            var factory = CreatePendingRequestQueueFactory(octopusLogFactory);

            var clientBuilder = new HalibutRuntimeBuilder()
                .WithServerCertificate(clientCertAndThumbprint.Certificate2)
                .WithLogFactory(octopusLogFactory)
                .WithPendingRequestQueueFactory(factory)
                .WithTrustProvider(clientTrustProvider)
                .WithAsyncHalibutFeatureEnabledIfForcingAsync(forceClientProxyType)
                .WithStreamFactoryIfNotNull(streamFactory)
                .WithHalibutTimeoutsAndLimits(halibutTimeoutsAndLimits)
                .WithOnUnauthorizedClientConnect(clientOnUnauthorizedClientConnect);

            if (clientRpcObserver is not null)
            {
                clientBuilder.WithRpcObserver(clientRpcObserver);
            }

            var client = clientBuilder.Build();
            client.Trust(clientTrustsThumbprint);

            HalibutRuntime? service = null;
            if (hasService)
            {
                var serviceBuilder = new HalibutRuntimeBuilder()
                    .WithServiceFactory(serviceFactory)
                    .WithServerCertificate(serviceCertAndThumbprint.Certificate2)
                    .WithAsyncHalibutFeature(serviceAsyncHalibutFeature)
                    .WithHalibutTimeoutsAndLimits(halibutTimeoutsAndLimits)
                    .WithStreamFactoryIfNotNull(streamFactory)
                    .WithLogFactory(BuildServiceLogger());

                if(pollingReconnectRetryPolicy != null) serviceBuilder.WithPollingReconnectRetryPolicy(pollingReconnectRetryPolicy);
                service = serviceBuilder.Build();
            }

            var disposableCollection = new DisposableCollection();
            PortForwarder? portForwarder = null;
            var httpProxy = proxyFactory?.WithDelaySendingSectionsOfHttpHeaders(ProxyDelaySendingSectionsOfHttpHeaders()).Build();
            ProxyDetails? httpProxyDetails = null;

            if (httpProxy != null)
            {
                await httpProxy.StartAsync();
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

                    service.Poll(serviceUri, new ServiceEndPoint(new Uri("https://localhost:" + clientListenPort), serviceTrustsThumbprint, httpProxyDetails, service.TimeoutsAndLimits));
                }
            }
            else if (serviceConnectionType == ServiceConnectionType.PollingOverWebSocket)
            {
                var webSocketListeningInfo = await TryListenWebSocket.WebSocketListeningPort(logger, client, cancellationToken);
                var webSocketListeningPort = webSocketListeningInfo.WebSocketListeningPort;

                var webSocketSslCertificate = new WebSocketSslCertificateBuilder(webSocketListeningInfo.WebSocketSslCertificateBindingAddress)
                    .WithCertificate(clientCertAndThumbprint)
                    .Build();
                disposableCollection.Add(webSocketSslCertificate);

                serviceUri = new Uri("poll://SQ-TENTAPOLL");
                if (service != null)
                {
                    portForwarder = portForwarderFactory?.Invoke(webSocketListeningInfo.WebSocketListeningPort);

                    if (portForwarder != null)
                    {
                        webSocketListeningPort = portForwarder.ListeningPort;
                    }

                    var webSocketServiceEndpointUri = new Uri($"wss://localhost:{webSocketListeningPort}/{webSocketListeningInfo.WebSocketPath}");
                    service.Poll(serviceUri, new ServiceEndPoint(webSocketServiceEndpointUri, serviceTrustsThumbprint, httpProxyDetails, service.TimeoutsAndLimits));
                }
            }
            else if (serviceConnectionType == ServiceConnectionType.Listening)
            {
                int listenPort;
                if (service != null)
                {
                    service.Trust(serviceTrustsThumbprint);
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

            return new ClientAndService(client, service, serviceUri, clientTrustsThumbprint, portForwarder, disposableCollection, httpProxy, httpProxyDetails, forceClientProxyType, cancellationTokenSource);
        }

        IPendingRequestQueueFactory CreatePendingRequestQueueFactory(ILogFactory octopusLogFactory)
        {
            if (pendingRequestQueueFactory != null)
            {
                return pendingRequestQueueFactory(octopusLogFactory);
            }

            var pendingRequestQueueFactoryBuilder = new PendingRequestQueueFactoryBuilder(octopusLogFactory)
                .WithSyncOrAsync(forceClientProxyType.ToSyncOrAsync());

            if (this.pendingRequestQueueFactoryBuilder != null)
            {
                this.pendingRequestQueueFactoryBuilder(pendingRequestQueueFactoryBuilder);
            }

            var factory = pendingRequestQueueFactoryBuilder.Build();
            return factory;
        }

        ILogFactory BuildClientLogger()
        {
            if (clientInMemoryLoggers == null)
            {
                return new TestContextLogCreator("Client", halibutLogLevel).ToCachingLogFactory();
            }



            return new AggregateLogWriterLogCreator(
                    new TestContextLogCreator("Client", halibutLogLevel),
                s =>
                {
                    var logger = new InMemoryLogWriter();
                    clientInMemoryLoggers[s] = logger;
                    return new[] {logger};
                }
            )
                .ToCachingLogFactory();
        }
        
        ILogFactory BuildServiceLogger()
        {
            if (serviceInMemoryLoggers == null)
            {
                return new TestContextLogCreator("Service", halibutLogLevel).ToCachingLogFactory();
            }

            return new AggregateLogWriterLogCreator(
                    new TestContextLogCreator("Service", halibutLogLevel),
                    s =>
                    {
                        var logger = new InMemoryLogWriter();
                        serviceInMemoryLoggers[s] = logger;
                        return new[] {logger};
                    }
                )
                .ToCachingLogFactory();
        }

        public class ClientAndService : IClientAndService
        {
            public Uri ServiceUri { get; }
            readonly string thumbprint;
            readonly DisposableCollection disposableCollection;
            readonly ProxyDetails? proxyDetails;
            readonly CancellationTokenSource cancellationTokenSource;
            readonly ForceClientProxyType? forceClientProxyType;

            public ClientAndService(
                HalibutRuntime client,
                HalibutRuntime service,
                Uri serviceUri,
                string thumbprint,
                PortForwarder? portForwarder,
                DisposableCollection disposableCollection,
                HttpProxyService? proxy,
                ProxyDetails? proxyDetails,
                ForceClientProxyType? forceClientProxyType,
                CancellationTokenSource cancellationTokenSource)
            {
                Client = client;
                Service = service;
                ServiceUri = serviceUri;
                this.thumbprint = thumbprint;
                PortForwarder = portForwarder;
                HttpProxy = proxy;
                this.disposableCollection = disposableCollection;
                this.proxyDetails = proxyDetails;
                this.cancellationTokenSource = cancellationTokenSource;
                this.forceClientProxyType = forceClientProxyType;
            }

            public HalibutRuntime Client { get; }
            public ServiceEndPoint ServiceEndPoint => GetServiceEndPoint();

            public HalibutRuntime? Service { get; }
            public PortForwarder? PortForwarder { get; }
            public HttpProxyService? HttpProxy { get; }

            public ServiceEndPoint GetServiceEndPoint()
            {
                return new ServiceEndPoint(ServiceUri, thumbprint, proxyDetails, Client.TimeoutsAndLimits);
            }

            public TService CreateClient<TService>(CancellationToken? cancellationToken = null)
            {
                return CreateClient<TService>(_ => { }, cancellationToken ?? CancellationToken.None);
            }

            public TService CreateClient<TService>(Action<ServiceEndPoint> modifyServiceEndpoint)
            {
                return CreateClient<TService>(modifyServiceEndpoint, CancellationToken.None);
            }

            public TService CreateClient<TService>(Action<ServiceEndPoint> modifyServiceEndpoint, CancellationToken cancellationToken)
            {
                var serviceEndpoint = GetServiceEndPoint();
                modifyServiceEndpoint(serviceEndpoint);

                return new AdaptToSyncOrAsyncTestCase().Adapt<TService>(forceClientProxyType, Client, serviceEndpoint, cancellationToken);
            }

            public TClientService CreateClient<TService, TClientService>()
            {
                return CreateClient<TService, TClientService>(_ => { });
            }

            public TClientService CreateClient<TService, TClientService>(Action<ServiceEndPoint> modifyServiceEndpoint)
            {
                var serviceEndpoint = GetServiceEndPoint();
                modifyServiceEndpoint(serviceEndpoint);
                return new AdaptToSyncOrAsyncTestCase().Adapt<TService, TClientService>(forceClientProxyType, Client, serviceEndpoint);
            }
            
            public TAsyncClientWithOptions CreateClientWithOptions<TService, TSyncClientWithOptions, TAsyncClientWithOptions>()
            {
                return CreateClientWithOptions<TService, TSyncClientWithOptions, TAsyncClientWithOptions>(_ => { });
            }

            public TAsyncClientWithOptions CreateClientWithOptions<TService, TSyncClientWithOptions, TAsyncClientWithOptions>(Action<ServiceEndPoint> modifyServiceEndpoint)
            {
                var serviceEndpoint = GetServiceEndPoint();
                modifyServiceEndpoint(serviceEndpoint);
                return new AdaptToSyncOrAsyncTestCase().Adapt<TService, TSyncClientWithOptions, TAsyncClientWithOptions>(forceClientProxyType, Client, serviceEndpoint);
            }
            
            public TAsyncClientService CreateAsyncClient<TService, TAsyncClientService>()
            {
                return Client.CreateAsyncClient<TService, TAsyncClientService>(ServiceEndPoint);
            }

            public async ValueTask DisposeAsync()
            {
                var logger = new SerilogLoggerBuilder().Build().ForContext<ClientAndService>();

                logger.Information("****** ****** ****** ****** ****** ****** ******");
                logger.Information("****** CLIENT AND SERVICE DISPOSE CALLED  ******");
                logger.Information("*     Subsequent errors should be ignored      *");
                logger.Information("****** ****** ****** ****** ****** ****** ******");

                void LogError(Exception e) => logger.Warning(e, "Ignoring error in dispose");

                Try.CatchingError(() => cancellationTokenSource.Cancel(), LogError);

                if (Client.AsyncHalibutFeature == AsyncHalibutFeature.Enabled)
                {
                    await Try.DisposingAsync(Client, LogError);
                }
                else
                {
                    Try.CatchingError(Client.Dispose, LogError);
                }

                if (Service is not null)
                {
                    if (Service.AsyncHalibutFeature == AsyncHalibutFeature.Enabled)
                    {
                        await Try.DisposingAsync(Service, LogError);
                    }
                    else
                    {
                        Try.CatchingError(Service.Dispose, LogError);
                    }
                }

                Try.CatchingError(() => HttpProxy?.Dispose(), LogError);
                Try.CatchingError(() => PortForwarder?.Dispose(), LogError);
                Try.CatchingError(disposableCollection.Dispose, LogError);
                Try.CatchingError(() => cancellationTokenSource.Dispose(), LogError);
            }
        }
    }
}
