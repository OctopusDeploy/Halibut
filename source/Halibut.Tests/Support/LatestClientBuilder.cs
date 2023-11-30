using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.Diagnostics.LogCreators;
using Halibut.Logging;
using Halibut.ServiceModel;
using Halibut.Tests.Support.Logging;
using Halibut.Transport.Observability;
using Halibut.Transport.Streams;
using Octopus.TestPortForwarder;
using ILog = Halibut.Diagnostics.ILog;

namespace Halibut.Tests.Support
{
    public class LatestClientBuilder : IClientOnlyBuilder
    {
        readonly ServiceConnectionType serviceConnectionType;

        readonly CertAndThumbprint clientCertAndThumbprint;

        string clientTrustsThumbprint; 
        IRpcObserver? clientRpcObserver;
        Func<int, PortForwarder>? portForwarderFactory;
        Reference<PortForwarder>? portForwarderReference;
        Func<ILogFactory, IPendingRequestQueueFactory>? pendingRequestQueueFactory;
        Action<PendingRequestQueueFactoryBuilder>? pendingRequestQueueFactoryBuilder;
        ProxyDetails? proxyDetails;
        LogLevel halibutLogLevel = LogLevel.Trace;
        ConcurrentDictionary<string, ILog>? clientInMemoryLoggers;
        ITrustProvider? clientTrustProvider;
        Func<string, string, UnauthorizedClientConnectResponse>? clientOnUnauthorizedClientConnect;
        HalibutTimeoutsAndLimits halibutTimeoutsAndLimits = new HalibutTimeoutsAndLimitsForTestsBuilder().Build();
        IStreamFactory? clientStreamFactory;
        IConnectionsObserver? clientConnectionsObserver;
        

        public LatestClientBuilder(
            ServiceConnectionType serviceConnectionType,
            CertAndThumbprint clientCertAndThumbprint,
            CertAndThumbprint serviceCertAndThumbprint)
        {
            this.serviceConnectionType = serviceConnectionType;
            this.clientCertAndThumbprint = clientCertAndThumbprint;
            clientTrustsThumbprint = serviceCertAndThumbprint.Thumbprint;
        }

        public static LatestClientBuilder Polling()
        {
            return new LatestClientBuilder(ServiceConnectionType.Polling, CertAndThumbprint.Octopus, CertAndThumbprint.TentaclePolling);
        }

        public static LatestClientBuilder PollingOverWebSocket()
        {
            return new LatestClientBuilder(ServiceConnectionType.PollingOverWebSocket, CertAndThumbprint.Ssl, CertAndThumbprint.TentaclePolling);
        }

        public static LatestClientBuilder Listening()
        {
            return new LatestClientBuilder(ServiceConnectionType.Listening, CertAndThumbprint.Octopus, CertAndThumbprint.TentacleListening);
        }

        public static LatestClientBuilder ForServiceConnectionType(ServiceConnectionType serviceConnectionType)
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

        public LatestClientBuilder WithStreamFactory(IStreamFactory streamFactory)
        {
            this.clientStreamFactory = streamFactory;
            return this;
        }
        
        public LatestClientBuilder WithClientConnectionsObserver(IConnectionsObserver connectionsObserver)
        {
            this.clientConnectionsObserver = connectionsObserver;
            return this;
        }
        
        public LatestClientBuilder WithHalibutTimeoutsAndLimits(HalibutTimeoutsAndLimits halibutTimeoutsAndLimits)
        {
            this.halibutTimeoutsAndLimits = halibutTimeoutsAndLimits;
            return this;
        }

        IClientOnlyBuilder IClientOnlyBuilder.WithPortForwarding(out Reference<PortForwarder> portForwarder, Func<int, PortForwarder> portForwarderFactory)
        {
            return WithPortForwarding(out portForwarder, portForwarderFactory);
        }

        public LatestClientBuilder WithPortForwarding(out Reference<PortForwarder> portForwarder, Func<int, PortForwarder> portForwarderFactory)
        {
            if (this.portForwarderFactory != null)
            {
                throw new NotSupportedException("A PortForwarderFactory is already registered with the Builder. Only one PortForwarder is supported.");
            }

            this.portForwarderFactory = portForwarderFactory;
            this.portForwarderReference = new Reference<PortForwarder>();
            portForwarder = this.portForwarderReference;
            return this;
        }
        
        public LatestClientBuilder WithPendingRequestQueueFactory(Func<ILogFactory, IPendingRequestQueueFactory> pendingRequestQueueFactory)
        {
            this.pendingRequestQueueFactory = pendingRequestQueueFactory;
            return this;
        }

        public LatestClientBuilder WithPendingRequestQueueFactoryBuilder(Action<PendingRequestQueueFactoryBuilder> pendingRequestQueueFactoryBuilder)
        {
            this.pendingRequestQueueFactoryBuilder = pendingRequestQueueFactoryBuilder;
            return this;
        }

        public LatestClientBuilder WithProxyDetails(ProxyDetails? proxyDetails)
        {
            this.proxyDetails = proxyDetails;
            return this;
        }

        IClientOnlyBuilder IClientOnlyBuilder.WithHalibutLoggingLevel(LogLevel halibutLogLevel)
        {
            return WithHalibutLoggingLevel(halibutLogLevel);
        }
        
        public LatestClientBuilder WithHalibutLoggingLevel(LogLevel halibutLogLevel)
        {
            this.halibutLogLevel = halibutLogLevel;
            return this;
        }
        
        public LatestClientBuilder RecordingClientLogs(out ConcurrentDictionary<string, ILog> inMemoryLoggers)
        {
            inMemoryLoggers = new ConcurrentDictionary<string, ILog>();
            this.clientInMemoryLoggers = inMemoryLoggers;
            return this;
        }
        
        public LatestClientBuilder WithClientOnUnauthorizedClientConnect(Func<string, string, UnauthorizedClientConnectResponse> onUnauthorizedClientConnect)
        {
            clientOnUnauthorizedClientConnect = onUnauthorizedClientConnect;
            return this;
        }
        
        public LatestClientBuilder WithClientTrustProvider(ITrustProvider trustProvider)
        {
            clientTrustProvider = trustProvider;
            return this;
        }

        public LatestClientBuilder WithClientTrustingTheWrongCertificate()
        {
            clientTrustsThumbprint = CertAndThumbprint.Wrong.Thumbprint;
            return this;
        }
        
        public LatestClientBuilder WithClientRpcObserver(IRpcObserver? clientRpcObserver)
        {
            this.clientRpcObserver = clientRpcObserver;
            return this;
        }

        async Task<IClientOnly> IClientOnlyBuilder.Build(CancellationToken cancellationToken)
        {
            return await Build(cancellationToken);
        }

        public async Task<ClientOnly> Build(CancellationToken cancellationToken)
        {
            var octopusLogFactory = BuildClientLogger();

            var factory = CreatePendingRequestQueueFactory(octopusLogFactory);

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

            var client = clientBuilder.Build();
            client.Trust(clientTrustsThumbprint);
            
            var disposableCollection = new DisposableCollection();
            PortForwarder? portForwarder = null;
            Uri? clientPollingUri = null;

            if (serviceConnectionType == ServiceConnectionType.Polling)
            {
                var clientListenPort = client!.Listen();

                portForwarder = portForwarderFactory?.Invoke(clientListenPort);
                
                clientPollingUri = new Uri($"https://localhost:{portForwarder?.ListeningPort ?? clientListenPort}");
            }
            else if (serviceConnectionType == ServiceConnectionType.PollingOverWebSocket)
            {
                var logger = new SerilogLoggerBuilder().Build().ForContext<LatestClientBuilder>();
                var webSocketListeningInfo = await TryListenWebSocket.WebSocketListeningPort(logger, client!, cancellationToken);

                var webSocketSslCertificate = new WebSocketSslCertificateBuilder(webSocketListeningInfo.WebSocketSslCertificateBindingAddress)
                    .WithCertificate(clientCertAndThumbprint)
                    .Build();
                disposableCollection.Add(webSocketSslCertificate);

                portForwarder = portForwarderFactory?.Invoke(webSocketListeningInfo.WebSocketListeningPort);

                clientPollingUri = new Uri($"wss://localhost:{portForwarder?.ListeningPort ?? webSocketListeningInfo.WebSocketListeningPort}/{webSocketListeningInfo.WebSocketPath}");
            }
            else if (serviceConnectionType == ServiceConnectionType.Listening)
            {
                //Do over in other builder?
                //var dummyTentacle = new TCPListenerWhichKillsNewConnections();
                //disposableCollection.Add(dummyTentacle);
                //var listenPort = dummyTentacle.Port;
                

                //portForwarder = portForwarderFactory?.Invoke(listenPort);
                //if (portForwarder != null)
                //{
                //    listenPort = portForwarder.ListeningPort;
                //}
            }
            else
            {
                throw new NotSupportedException();
            }

            if (portForwarderReference != null && portForwarder != null)
            {
                portForwarderReference.Value = portForwarder;
            }

            return new ClientOnly(client, clientPollingUri, clientTrustsThumbprint, portForwarder, proxyDetails, serviceConnectionType, disposableCollection);
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
        
        public class ClientOnly : IClientOnly
        {
            readonly string thumbprint;
            readonly PortForwarder? portForwarder;
            readonly ProxyDetails? proxyDetails;
            readonly ServiceConnectionType serviceConnectionType;
            readonly DisposableCollection disposableCollection;

            public ClientOnly(HalibutRuntime client,
                Uri? pollingUri,
                string thumbprint,
                PortForwarder? portForwarder,
                ProxyDetails? proxyDetails,
                ServiceConnectionType serviceConnectionType,
                DisposableCollection disposableCollection)
            {
                Client = client;
                PollingUri = pollingUri;
                this.thumbprint = thumbprint;
                this.portForwarder = portForwarder;
                this.proxyDetails = proxyDetails;
                this.serviceConnectionType = serviceConnectionType;
                this.disposableCollection = disposableCollection;
            }

            public HalibutRuntime Client { get; }
            public Uri? PollingUri { get; }
            
            public TAsyncClientService CreateClient<TService, TAsyncClientService>(Uri serviceUri)
            {
                var serviceEndPoint = GetServiceEndPoint(serviceUri);
                return Client.CreateAsyncClient<TService, TAsyncClientService>(serviceEndPoint);
            }

            public TAsyncClientService CreateClient<TService, TAsyncClientService>(Uri serviceUri, Action<ServiceEndPoint> modifyServiceEndpoint)
            {
                var serviceEndPoint = GetServiceEndPoint(serviceUri);
                modifyServiceEndpoint(serviceEndPoint);
                return Client.CreateAsyncClient<TService, TAsyncClientService>(serviceEndPoint);
            }

            public TAsyncClientService CreateClientWithoutService<TService, TAsyncClientService>()
            {
                var serviceThatDoesNotExistUri = ServiceUriThatDoesNotExist();
                return CreateClient<TService, TAsyncClientService>(serviceThatDoesNotExistUri);
            }

            public TAsyncClientService CreateClientWithoutService<TService, TAsyncClientService>(Action<ServiceEndPoint> modifyServiceEndpoint)
            {
                var serviceThatDoesNotExistUri = ServiceUriThatDoesNotExist();
                var serviceEndPoint = GetServiceEndPoint(serviceThatDoesNotExistUri);
                modifyServiceEndpoint(serviceEndPoint);
                return Client.CreateAsyncClient<TService, TAsyncClientService>(serviceEndPoint);
            }

            public ServiceEndPoint GetServiceEndPoint(Uri serviceUri)
            {
                var serviceEndPoint = new ServiceEndPoint(serviceUri, thumbprint, proxyDetails, Client.TimeoutsAndLimits);
                return serviceEndPoint;
            }

            Uri ServiceUriThatDoesNotExist()
            {
                switch (serviceConnectionType)
                {
                    case ServiceConnectionType.Polling:
                        return LatestServiceBuilder.PollingTentacleServiceUri;
                    case ServiceConnectionType.PollingOverWebSocket:
                        return LatestServiceBuilder.PollingOverWebSocketTentacleServiceUri;
                    case ServiceConnectionType.Listening:
                        return LatestServiceBuilder.ListeningTentacleServiceUri(51234);
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            public async ValueTask DisposeAsync()
            {
                var logger = new SerilogLoggerBuilder().Build().ForContext<ClientOnly>();

                logger.Information("****** ****** ****** ****** ****** ****** ******");
                logger.Information("****** CLIENT DISPOSE CALLED  ******");
                logger.Information("*     Subsequent errors should be ignored      *");
                logger.Information("****** ****** ****** ****** ****** ****** ******");

                void LogError(Exception e) => logger.Warning(e, "Ignoring error in dispose");
                
                await Try.DisposingAsync(Client, LogError);
                
                Try.CatchingError(() => portForwarder?.Dispose(), LogError);
                Try.CatchingError(disposableCollection.Dispose, LogError);
            }
        }
    }
}
