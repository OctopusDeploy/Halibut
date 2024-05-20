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
    public class LatestClientBuilder : IClientBuilder
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
        IControlMessageObserver? controlMessageObserver;

        public LatestClientBuilder(
            ServiceConnectionType serviceConnectionType,
            CertAndThumbprint clientCertAndThumbprint,
            CertAndThumbprint serviceCertAndThumbprint)
        {
            this.serviceConnectionType = serviceConnectionType;
            this.clientCertAndThumbprint = clientCertAndThumbprint;
            clientTrustsThumbprint = serviceCertAndThumbprint.Thumbprint;
        }

        public static LatestClientBuilder ForServiceConnectionType(ServiceConnectionType serviceConnectionType)
        {
            switch (serviceConnectionType)
            {
                case ServiceConnectionType.Polling:
                    return new LatestClientBuilder(ServiceConnectionType.Polling, CertAndThumbprint.Octopus, CertAndThumbprint.TentaclePolling);
                case ServiceConnectionType.Listening:
                    return new LatestClientBuilder(ServiceConnectionType.Listening, CertAndThumbprint.Octopus, CertAndThumbprint.TentacleListening);
                case ServiceConnectionType.PollingOverWebSocket:
                    return new LatestClientBuilder(ServiceConnectionType.PollingOverWebSocket, CertAndThumbprint.Ssl, CertAndThumbprint.TentaclePolling);
                default:
                    throw new ArgumentOutOfRangeException(nameof(serviceConnectionType), serviceConnectionType, null);
            }
        }

        public LatestClientBuilder WithStreamFactory(IStreamFactory streamFactory)
        {
            this.clientStreamFactory = streamFactory;
            return this;
        }

        public LatestClientBuilder WithControlMessageObserver(IControlMessageObserver controlMessageObserver)
        {
            this.controlMessageObserver = controlMessageObserver;
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

        IClientBuilder IClientBuilder.WithPortForwarding(out Reference<PortForwarder> portForwarder, Func<int, PortForwarder> portForwarderFactory)
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

        IClientBuilder IClientBuilder.WithHalibutLoggingLevel(LogLevel halibutLogLevel)
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

        async Task<IClient> IClientBuilder.Build(CancellationToken cancellationToken)
        {
            return await Build(cancellationToken);
        }

        public async Task<LatestClient> Build(CancellationToken cancellationToken)
        {
            var octopusLogFactory = BuildClientLogger();

            var factory = CreatePendingRequestQueueFactory(octopusLogFactory);

            var clientBuilder = new HalibutRuntimeBuilder()
                .WithServerCertificate(clientCertAndThumbprint.Certificate2)
                .WithLogFactory(octopusLogFactory)
                .WithPendingRequestQueueFactory(factory)
                .WithTrustProvider(clientTrustProvider!)
                .WithStreamFactoryIfNotNull(clientStreamFactory)
                .WithControlMessageObserverIfNotNull(controlMessageObserver)
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
            Uri? clientListeningUri = null;

            if (serviceConnectionType == ServiceConnectionType.Polling)
            {
                var clientListenPort = client!.Listen();

                portForwarder = portForwarderFactory?.Invoke(clientListenPort);
                
                clientListeningUri = new Uri($"https://localhost:{portForwarder?.ListeningPort ?? clientListenPort}");
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

                clientListeningUri = new Uri($"wss://localhost:{portForwarder?.ListeningPort ?? webSocketListeningInfo.WebSocketListeningPort}/{webSocketListeningInfo.WebSocketPath}");
            }
            else if (serviceConnectionType == ServiceConnectionType.Listening)
            {
                //Nothing to do if we are listening (except not throw an NotSupportedException)
            }
            else
            {
                throw new NotSupportedException();
            }

            if (portForwarderReference != null && portForwarder != null)
            {
                portForwarderReference.Value = portForwarder;
            }

            return new LatestClient(client, clientListeningUri, clientTrustsThumbprint, portForwarder, proxyDetails, serviceConnectionType, disposableCollection);
        }

        IPendingRequestQueueFactory CreatePendingRequestQueueFactory(ILogFactory octopusLogFactory)
        {
            if (pendingRequestQueueFactory != null)
            {
                return pendingRequestQueueFactory(octopusLogFactory);
            }

            var pendingRequestQueueFactoryBuilder = new PendingRequestQueueFactoryBuilder(octopusLogFactory, halibutTimeoutsAndLimits);

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
    }
}
