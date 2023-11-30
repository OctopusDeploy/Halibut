#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics.LogCreators;
using Halibut.Logging;
using Halibut.TestProxy;
using Halibut.Tests.Support.Logging;
using Halibut.Transport.Proxy;
using Octopus.TestPortForwarder;

namespace Halibut.Tests.Support.BackwardsCompatibility
{
    public class LatestClientAndPreviousServiceVersionBuilder : IClientAndServiceBuilder
    {
        readonly ServiceConnectionType serviceConnectionType;
        readonly CertAndThumbprint serviceCertAndThumbprint;
        readonly CertAndThumbprint clientCertAndThumbprint = CertAndThumbprint.Octopus;
        Version? version = null;
        Func<int, PortForwarder>? portForwarderFactory;
        Reference<PortForwarder>? portForwarderReference;
        ProxyFactory? proxyFactory;
        LogLevel halibutLogLevel = LogLevel.Trace;
        OldServiceAvailableServices availableServices = new(false, false);
        bool hasService = true;

        LatestClientAndPreviousServiceVersionBuilder(ServiceConnectionType serviceConnectionType, CertAndThumbprint serviceCertAndThumbprint)
        {
            this.serviceConnectionType = serviceConnectionType;
            this.serviceCertAndThumbprint = serviceCertAndThumbprint;
        }

        public static LatestClientAndPreviousServiceVersionBuilder WithPollingService()
        {
            return new LatestClientAndPreviousServiceVersionBuilder(ServiceConnectionType.Polling, CertAndThumbprint.TentaclePolling);
        }

        public static LatestClientAndPreviousServiceVersionBuilder WithPollingOverWebSocketService()
        {
            return new LatestClientAndPreviousServiceVersionBuilder(ServiceConnectionType.PollingOverWebSocket, CertAndThumbprint.TentaclePolling);
        }

        public static LatestClientAndPreviousServiceVersionBuilder WithListeningService()
        {
            return new LatestClientAndPreviousServiceVersionBuilder(ServiceConnectionType.Listening, CertAndThumbprint.TentacleListening);
        }

        public static LatestClientAndPreviousServiceVersionBuilder ForServiceConnectionType(ServiceConnectionType connectionType)
        {
            switch (connectionType)
            {
                case ServiceConnectionType.Polling:
                    return WithPollingService();
                case ServiceConnectionType.PollingOverWebSocket:
                    return WithPollingOverWebSocketService();
                case ServiceConnectionType.Listening:
                    return WithListeningService();
                default:
                    throw new ArgumentOutOfRangeException(nameof(connectionType), connectionType, null);
            }
        }

        public LatestClientAndPreviousServiceVersionBuilder WithServiceVersion(Version? version)
        {
            this.version = version;
            return this;
        }

        IClientAndServiceBuilder IClientAndServiceBuilder.WithPortForwarding(Func<int, PortForwarder> portForwarderFactory)
        {
            return WithPortForwarding(portForwarderFactory);
        }

        public LatestClientAndPreviousServiceVersionBuilder WithPortForwarding(Func<int, PortForwarder> portForwarderFactory)
        {
            if (this.portForwarderFactory != null)
            {
                throw new NotSupportedException("A PortForwarderFactory is already registered with the Builder. Only one PortForwarder is supported");
            }

            this.portForwarderFactory = portForwarderFactory;
            return this;
        }

        IClientAndServiceBuilder IClientAndServiceBuilder.WithPortForwarding(out Reference<PortForwarder> portForwarder, Func<int, PortForwarder> portForwarderFactory)
        {
            return WithPortForwarding(portForwarderFactory);
        }

        public LatestClientAndPreviousServiceVersionBuilder WithPortForwarding(out Reference<PortForwarder> portForwarder, Func<int, PortForwarder> portForwarderFactory)
        {
            if (this.portForwarderFactory != null)
            {
                throw new NotSupportedException("A PortForwarderFactory is already registered with the Builder. Only one PortForwarder is supported");
            }

            this.portForwarderFactory = portForwarderFactory;
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

        public LatestClientAndPreviousServiceVersionBuilder WithStandardServices()
        {
            availableServices.HasStandardServices = true;
            return this;
        }

        public LatestClientAndPreviousServiceVersionBuilder WithTentacleServices()
        {
            availableServices.HasTentacleServices = true;
            return this;
        }

        IClientAndServiceBuilder IClientAndServiceBuilder.WithCachingService() => WithCachingService();
        
        public IClientAndServiceBuilder WithCachingService()
        {
            availableServices.HasCachingService = true;
            return this;
        }

        IClientAndServiceBuilder IClientAndServiceBuilder.WithProxy()
        {
            return WithProxy();
        }

        public LatestClientAndPreviousServiceVersionBuilder WithProxy()
        {
            this.proxyFactory = new ProxyFactory().WithDelaySendingSectionsOfHttpHeaders(false);

            return this;
        }

        IClientAndServiceBuilder IClientAndServiceBuilder.WithHalibutLoggingLevel(LogLevel halibutLogLevel)
        {
            return WithHalibutLoggingLevel(halibutLogLevel);
        }

        public LatestClientAndPreviousServiceVersionBuilder WithHalibutLoggingLevel(LogLevel halibutLogLevel)
        {
            this.halibutLogLevel = halibutLogLevel;

            return this;
        }

        /// <summary>
        ///     Ie no tentacle.
        ///     In the case of listening, a TCPListenerWhichKillsNewConnections will be created. This will cause connections to
        ///     that port to be killed immediately.
        /// </summary>
        /// <returns></returns>
        public LatestClientAndPreviousServiceVersionBuilder NoService()
        {
            hasService = false;
            return this;
        }

        async Task<IClientAndService> IClientAndServiceBuilder.Build(CancellationToken cancellationToken)
        {
            return await Build(cancellationToken);
        }
        
        public async Task<ClientAndService> Build(CancellationToken cancellationToken)
        {
            var logger = new SerilogLoggerBuilder().Build().ForContext<LatestClientAndPreviousServiceVersionBuilder>();
            CancellationTokenSource cancellationTokenSource = new();
            if (version == null)
            {
                throw new Exception("The version of the service must be set.");
            }

            var clientBuilder = new HalibutRuntimeBuilder()
                .WithServerCertificate(clientCertAndThumbprint.Certificate2)
                .WithLogFactory(new TestContextLogCreator("Client", halibutLogLevel).ToCachingLogFactory())
                .WithHalibutTimeoutsAndLimits(new HalibutTimeoutsAndLimitsForTestsBuilder().Build());

            var client = clientBuilder.Build();
            client.Trust(serviceCertAndThumbprint.Thumbprint);

            HalibutTestBinaryRunner.RunningOldHalibutBinary? runningOldHalibutBinary = null;
            var disposableCollection = new DisposableCollection();
            PortForwarder? portForwarder = null;

            var proxy = proxyFactory?.Build();
            ProxyDetails? proxyDetails = null;

            if (proxy != null)
            {
                await proxy.StartAsync();
                proxyDetails = new ProxyDetails("localhost", proxy.Endpoint!.Port, ProxyType.HTTP);
            }

            Uri serviceUri;

            if (serviceConnectionType == ServiceConnectionType.Polling)
            {
                var listenPort = client.Listen();
                portForwarder = portForwarderFactory?.Invoke(listenPort);
                serviceUri = new Uri("poll://SQ-TENTAPOLL");

                if (portForwarder != null)
                {
                    listenPort = portForwarder.ListeningPort;
                }

                if (hasService)
                {
                    runningOldHalibutBinary = await new HalibutTestBinaryRunner(
                        serviceConnectionType,
                        listenPort,
                        clientCertAndThumbprint,
                        serviceCertAndThumbprint,
                        version?.ToString(),
                        proxyDetails!,
                        halibutLogLevel,
                        availableServices,
                        logger).Run();
                }
            }
            else if (serviceConnectionType == ServiceConnectionType.PollingOverWebSocket)
            {
                var webSocketListeningInfo = await TryListenWebSocket.WebSocketListeningPort(logger, client, cancellationToken);

                var webSocketListeningPort = webSocketListeningInfo.WebSocketListeningPort;
                portForwarder = portForwarderFactory?.Invoke(webSocketListeningPort);

                var webSocketSslCertificate = new WebSocketSslCertificateBuilder(webSocketListeningInfo.WebSocketSslCertificateBindingAddress).Build();
                disposableCollection.Add(webSocketSslCertificate);

                serviceUri = new Uri("poll://SQ-TENTAPOLL");

                if (portForwarder != null)
                {
                    webSocketListeningPort = portForwarder.ListeningPort;
                }

                if (hasService)
                {
                    var webSocketServiceEndpointUri = new Uri($"wss://localhost:{webSocketListeningPort}/{webSocketListeningInfo.WebSocketPath}");
                    runningOldHalibutBinary = await new HalibutTestBinaryRunner(
                        serviceConnectionType,
                        webSocketServiceEndpointUri,
                        clientCertAndThumbprint,
                        serviceCertAndThumbprint,
                        version?.ToString(),
                        proxyDetails,
                        halibutLogLevel,
                        availableServices,
                        logger).Run();
                }
            }
            else if (serviceConnectionType == ServiceConnectionType.Listening)
            {
                var listenPort = 6660666;

                if (hasService)
                {
                    runningOldHalibutBinary = await new HalibutTestBinaryRunner(
                        serviceConnectionType,
                        clientCertAndThumbprint,
                        serviceCertAndThumbprint,
                        version?.ToString(),
                        proxyDetails,
                        halibutLogLevel,
                        availableServices,
                        logger).Run();

                    listenPort = (int)runningOldHalibutBinary.ServiceListenPort!;

                    portForwarder = portForwarderFactory?.Invoke(listenPort);

                    if (portForwarder != null) listenPort = portForwarder.ListeningPort;
                }

                serviceUri = new Uri("https://localhost:" + listenPort);
            }
            else
            {
                throw new NotSupportedException();
            }

            return new ClientAndService(client, runningOldHalibutBinary, serviceUri, serviceCertAndThumbprint, portForwarder, disposableCollection, proxy, proxyDetails, cancellationTokenSource);
        }

        public class ClientAndService : IClientAndService
        {
            readonly HalibutTestBinaryRunner.RunningOldHalibutBinary? runningOldHalibutBinary;
            readonly Uri serviceUri;
            readonly CertAndThumbprint serviceCertAndThumbprint; // for creating a client
            readonly DisposableCollection disposableCollection;
            readonly ProxyDetails? proxyDetails;
            readonly CancellationTokenSource cancellationTokenSource;
            readonly PortForwarder? portForwarder;

            public ClientAndService(
                HalibutRuntime client,
                HalibutTestBinaryRunner.RunningOldHalibutBinary? runningOldHalibutBinary,
                Uri serviceUri,
                CertAndThumbprint serviceCertAndThumbprint,
                PortForwarder? portForwarder,
                DisposableCollection disposableCollection,
                HttpProxyService? httpProxy,
                ProxyDetails? proxyDetails,
                CancellationTokenSource cancellationTokenSource)
            {
                Client = client;
                this.runningOldHalibutBinary = runningOldHalibutBinary;
                this.serviceUri = serviceUri;
                this.serviceCertAndThumbprint = serviceCertAndThumbprint;
                this.portForwarder = portForwarder;
                HttpProxy = httpProxy;
                this.disposableCollection = disposableCollection;
                this.proxyDetails = proxyDetails;
                this.cancellationTokenSource = cancellationTokenSource;
            }

            public HalibutRuntime Client { get; }
            public ServiceEndPoint ServiceEndPoint => new(serviceUri, serviceCertAndThumbprint.Thumbprint, proxyDetails, Client.TimeoutsAndLimits);
            
            public HttpProxyService? HttpProxy { get; }
            
            public TAsyncClientService CreateAsyncClient<TService, TAsyncClientService>(Action<ServiceEndPoint> modifyServiceEndpoint)
            {
                var serviceEndpoint = ServiceEndPoint;
                modifyServiceEndpoint(serviceEndpoint);
                return Client.CreateAsyncClient<TService, TAsyncClientService>(serviceEndpoint);
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
                await Try.DisposingAsync(Client, LogError);
                Try.CatchingError(() => runningOldHalibutBinary?.Dispose(), LogError);
                Try.CatchingError(() => HttpProxy?.Dispose(), LogError);
                Try.CatchingError(() => portForwarder?.Dispose(), LogError);
                Try.CatchingError(() => disposableCollection.Dispose(), LogError);
                Try.CatchingError(() => cancellationTokenSource.Dispose(), LogError);
            }
        }
    }
}
