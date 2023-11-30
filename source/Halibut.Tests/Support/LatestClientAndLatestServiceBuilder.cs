using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.Diagnostics.LogCreators;
using Halibut.Logging;
using Halibut.ServiceModel;
using Halibut.TestProxy;
using Halibut.Tests.Builders;
using Halibut.Tests.Support.Logging;
using Halibut.Tests.TestServices;
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
using ICachingService = Halibut.TestUtils.Contracts.ICachingService;
using ILog = Halibut.Diagnostics.ILog;

namespace Halibut.Tests.Support
{
    public class LatestClientAndLatestServiceBuilder : IClientAndServiceBuilder
    {
        public ServiceConnectionType ServiceConnectionType { get; }

        ServiceFactoryBuilder serviceFactoryBuilder = new();
        IServiceFactory? serviceFactory;
        readonly CertAndThumbprint serviceCertAndThumbprint;
        string clientTrustsThumbprint; 
        readonly CertAndThumbprint clientCertAndThumbprint;
        string serviceTrustsThumbprint;
        IRpcObserver? clientRpcObserver;


        bool createService = true;
        bool createClient = true;
        readonly List<Uri> pollingClientUris = new();
        Func<int, PortForwarder>? portForwarderFactory;
        Func<ILogFactory, IPendingRequestQueueFactory>? pendingRequestQueueFactory;
        Action<PendingRequestQueueFactoryBuilder>? pendingRequestQueueFactoryBuilder;
        Reference<PortForwarder>? portForwarderReference;
        Func<RetryPolicy>? pollingReconnectRetryPolicy;
        ProxyFactory? proxyFactory;
        LogLevel halibutLogLevel = LogLevel.Trace;
        ConcurrentDictionary<string, ILog>? clientInMemoryLoggers;
        ConcurrentDictionary<string, ILog>? serviceInMemoryLoggers;
        ITrustProvider? clientTrustProvider;
        Func<string, string, UnauthorizedClientConnectResponse>? clientOnUnauthorizedClientConnect;
        HalibutTimeoutsAndLimits halibutTimeoutsAndLimits = new HalibutTimeoutsAndLimitsForTestsBuilder().Build();

        IStreamFactory? clientStreamFactory;
        IStreamFactory? serviceStreamFactory;
        IConnectionsObserver? serviceConnectionsObserver;
        IConnectionsObserver? clientConnectionsObserver;

        public LatestClientAndLatestServiceBuilder(
            ServiceConnectionType serviceConnectionType,
            CertAndThumbprint clientCertAndThumbprint,
            CertAndThumbprint serviceCertAndThumbprint)
        {
            this.ServiceConnectionType = serviceConnectionType;
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
            createService = false;
            return this;
        }

        IClientAndServiceBuilder IClientAndServiceBuilder.NoService()
        {
            return NoService();
        }
        
        public LatestClientAndLatestServiceBuilder WithPollingClients(IEnumerable<Uri> pollingClientUris)
        {
            createClient = false;
            this.pollingClientUris.AddRange(pollingClientUris);

            return this;
        }

        public LatestClientAndLatestServiceBuilder WithStreamFactory(IStreamFactory streamFactory)
        {
            this.serviceStreamFactory = streamFactory;
            this.clientStreamFactory = streamFactory;
            return this;
        }
        
        public LatestClientAndLatestServiceBuilder WithClientStreamFactory(IStreamFactory clientStreamFactory)
        {
            this.clientStreamFactory = clientStreamFactory;
            return this;
        }
        
        public LatestClientAndLatestServiceBuilder WithServiceStreamFactory(IStreamFactory serviceStreamFactory)
        {
            this.serviceStreamFactory = serviceStreamFactory;
            return this;
        }

        public LatestClientAndLatestServiceBuilder WithServiceConnectionsObserver(IConnectionsObserver connectionsObserver)
        {
            this.serviceConnectionsObserver = connectionsObserver;
            return this;
        }
        
        public LatestClientAndLatestServiceBuilder WithClientConnectionsObserver(IConnectionsObserver connectionsObserver)
        {
            this.clientConnectionsObserver = connectionsObserver;
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
                throw new NotSupportedException("A PortForwarderFactory is already registered with the Builder. Only one PortForwarder is supported.");
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
                .WithAsyncService<IFileTransferService, IAsyncFileTransferService>(() => new AsyncFileTransferService())
                .WithAsyncService<IScriptService, IAsyncScriptService>(() => new AsyncScriptService())
                .WithAsyncService<IScriptServiceV2, IAsyncScriptServiceV2>(() => new AsyncScriptServiceV2())
                .WithAsyncService<ICapabilitiesServiceV2, IAsyncCapabilitiesServiceV2>(() => new AsyncCapabilitiesServiceV2());
        }

        IClientAndServiceBuilder IClientAndServiceBuilder.WithCachingService() => WithCachingService();
        
        public LatestClientAndLatestServiceBuilder WithCachingService()
        {
            return this.WithAsyncService<ICachingService, IAsyncCachingService>(() => new AsyncCachingService());
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

            HalibutRuntime? client = null;
            if (createClient)
            {
                var clientBuilder = new HalibutRuntimeBuilder()
                    .WithServerCertificate(clientCertAndThumbprint.Certificate2)
                    .WithLogFactory(octopusLogFactory)
                    .WithPendingRequestQueueFactory(factory)
                    .WithTrustProvider(clientTrustProvider!)
                    .WithStreamFactoryIfNotNull(clientStreamFactory)
                    .WithConnectionsObserver(clientConnectionsObserver!)
                    .WithHalibutTimeoutsAndLimits(halibutTimeoutsAndLimits)
                    .WithOnUnauthorizedClientConnect(clientOnUnauthorizedClientConnect!);

                if (clientRpcObserver is not null)
                {
                    clientBuilder.WithRpcObserver(clientRpcObserver);
                }

                client = clientBuilder.Build();
                client.Trust(clientTrustsThumbprint);
            }
            

            HalibutRuntime? service = null;
            if (createService)
            {
                var serviceBuilder = new HalibutRuntimeBuilder()
                    .WithServiceFactory(serviceFactory)
                    .WithServerCertificate(serviceCertAndThumbprint.Certificate2)
                    .WithHalibutTimeoutsAndLimits(halibutTimeoutsAndLimits)
                    .WithStreamFactoryIfNotNull(serviceStreamFactory)
                    .WithConnectionsObserver(serviceConnectionsObserver!)
                    .WithLogFactory(BuildServiceLogger());

                if(pollingReconnectRetryPolicy != null) serviceBuilder.WithPollingReconnectRetryPolicy(pollingReconnectRetryPolicy);
                service = serviceBuilder.Build();
            }

            var disposableCollection = new DisposableCollection();
            PortForwarder? portForwarder = null;
            var httpProxy = proxyFactory?.WithDelaySendingSectionsOfHttpHeaders(true).Build();
            ProxyDetails? httpProxyDetails = null;

            if (httpProxy != null)
            {
                await httpProxy.StartAsync();
                httpProxyDetails = new ProxyDetails("localhost", httpProxy.Endpoint!.Port, ProxyType.HTTP);
            }

            Uri serviceUri;
            Uri? clientUri = null;
            
            if (ServiceConnectionType == ServiceConnectionType.Polling)
            {
                serviceUri = new Uri("poll://SQ-TENTAPOLL");

                var clientUrisToPoll = pollingClientUris.ToList();
                if (createClient)
                {
                    var clientListenPort = client!.Listen();

                    portForwarder = portForwarderFactory?.Invoke(clientListenPort);
                    
                    clientUri = new Uri($"https://localhost:{portForwarder?.ListeningPort ?? clientListenPort}");
                    clientUrisToPoll.Add(clientUri);
                }
                
                if (service != null)
                {
                    foreach (var clientUriToPoll in clientUrisToPoll)
                    {
                        service.Poll(
                            serviceUri,
                            new ServiceEndPoint(clientUriToPoll, serviceTrustsThumbprint, httpProxyDetails, service.TimeoutsAndLimits),
                            CancellationToken.None);
                    }
                }
            }
            else if (ServiceConnectionType == ServiceConnectionType.PollingOverWebSocket)
            {
                serviceUri = new Uri("poll://SQ-TENTAPOLL");
                
                var clientUrsToPoll = pollingClientUris.ToList();
                if (createClient)
                {
                    var webSocketListeningInfo = await TryListenWebSocket.WebSocketListeningPort(logger, client!, cancellationToken);

                    var webSocketSslCertificate = new WebSocketSslCertificateBuilder(webSocketListeningInfo.WebSocketSslCertificateBindingAddress)
                        .WithCertificate(clientCertAndThumbprint)
                        .Build();
                    disposableCollection.Add(webSocketSslCertificate);

                    portForwarder = portForwarderFactory?.Invoke(webSocketListeningInfo.WebSocketListeningPort);

                    clientUri = new Uri($"wss://localhost:{portForwarder?.ListeningPort ?? webSocketListeningInfo.WebSocketListeningPort}/{webSocketListeningInfo.WebSocketPath}");
                    clientUrsToPoll.Add(clientUri);
                }

                if (service != null)
                {
                    foreach (var clientUriToPoll in clientUrsToPoll)
                    {
                        service.Poll(
                            serviceUri,
                            new ServiceEndPoint(clientUriToPoll, serviceTrustsThumbprint, httpProxyDetails, service.TimeoutsAndLimits),
                            CancellationToken.None);
                    }
                }
            }
            else if (ServiceConnectionType == ServiceConnectionType.Listening)
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

            return new ClientAndService(client, clientUri, service, serviceUri, clientTrustsThumbprint, portForwarder, disposableCollection, httpProxy, httpProxyDetails, cancellationTokenSource);
        }

        IPendingRequestQueueFactory CreatePendingRequestQueueFactory(ILogFactory octopusLogFactory)
        {
            if (pendingRequestQueueFactory != null)
            {
                return pendingRequestQueueFactory(octopusLogFactory);
            }

            var pendingRequestQueueFactoryBuilder = new PendingRequestQueueFactoryBuilder(octopusLogFactory);

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

            public ClientAndService(
                HalibutRuntime? client,
                Uri? clientUri,
                HalibutRuntime? service,
                Uri serviceUri,
                string thumbprint,
                PortForwarder? portForwarder,
                DisposableCollection disposableCollection,
                HttpProxyService? proxy,
                ProxyDetails? proxyDetails,
                CancellationTokenSource cancellationTokenSource)
            {
                Client = client;
                ClientUri = clientUri;
                Service = service;
                ServiceUri = serviceUri;
                this.thumbprint = thumbprint;
                PortForwarder = portForwarder;
                HttpProxy = proxy;
                this.disposableCollection = disposableCollection;
                this.proxyDetails = proxyDetails;
                this.cancellationTokenSource = cancellationTokenSource;
            }

            public HalibutRuntime? Client { get; }
            public Uri? ClientUri { get; }
            public ServiceEndPoint ServiceEndPoint => GetServiceEndPoint();

            public HalibutRuntime? Service { get; }
            public PortForwarder? PortForwarder { get; }
            public HttpProxyService? HttpProxy { get; }

            public ServiceEndPoint GetServiceEndPoint()
            {
                return new ServiceEndPoint(ServiceUri, thumbprint, proxyDetails, Client!.TimeoutsAndLimits);
            }
            
            public TAsyncClientService CreateAsyncClient<TService, TAsyncClientService>(Action<ServiceEndPoint> modifyServiceEndpoint)
            {
                var serviceEndpoint = GetServiceEndPoint();
                modifyServiceEndpoint(serviceEndpoint);
                return Client!.CreateAsyncClient<TService, TAsyncClientService>(serviceEndpoint);
            }
            
            public TAsyncClientService CreateAsyncClient<TService, TAsyncClientService>()
            {
                return Client!.CreateAsyncClient<TService, TAsyncClientService>(ServiceEndPoint);
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

                if (Client is not null)
                {
                    await Try.DisposingAsync(Client, LogError);
                }

                if (Service is not null)
                {
                    await Try.DisposingAsync(Service, LogError);
                }

                Try.CatchingError(() => HttpProxy?.Dispose(), LogError);
                Try.CatchingError(() => PortForwarder?.Dispose(), LogError);
                Try.CatchingError(disposableCollection.Dispose, LogError);
                Try.CatchingError(() => cancellationTokenSource.Dispose(), LogError);
            }
        }
    }
}
