using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Logging;
using Halibut.TestProxy;
using Halibut.Transport.Proxy;
using Octopus.TestPortForwarder;
using Serilog.Extensions.Logging;

namespace Halibut.Tests.Support.BackwardsCompatibility
{
    public class LatestClientAndPreviousServiceVersionBuilder : IClientAndServiceBuilder
    {
        readonly ServiceConnectionType serviceConnectionType;
        readonly CertAndThumbprint serviceCertAndThumbprint;
        readonly CertAndThumbprint clientCertAndThumbprint = CertAndThumbprint.Octopus;
        string? version = null;
        Func<int, PortForwarder>? portForwarderFactory;
        Func<HttpProxyService>? proxyFactory;
        LogLevel halibutLogLevel;
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

        public LatestClientAndPreviousServiceVersionBuilder WithServiceVersion(Version version)
        {
            this.version = version.ToString();
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
            this.proxyFactory = () =>
            {
                var options = new HttpProxyOptions();
                var loggerFactory = new SerilogLoggerFactory(new SerilogLoggerBuilder().Build());

                return new HttpProxyService(options, loggerFactory);
            };

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

        IClientAndServiceBuilder IClientAndServiceBuilder.NoService()
        {
            return NoService();
        }

        async Task<IClientAndService> IClientAndServiceBuilder.Build(CancellationToken cancellationToken)
        {
            return await Build(cancellationToken);
        }
        
        public async Task<ClientAndService> Build(CancellationToken cancellationToken)
        {
            CancellationTokenSource cancellationTokenSource = new();
            if (version == null)
            {
                throw new Exception("The version of the service must be set.");
            }

            var octopus = new HalibutRuntime(clientCertAndThumbprint.Certificate2);
            octopus.Trust(serviceCertAndThumbprint.Thumbprint);

            HalibutTestBinaryRunner.RunningOldHalibutBinary? runningOldHalibutBinary = null;
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
                        version,
                        proxyDetails,
                        halibutLogLevel,
                        availableServices).Run();
                }
            }
            else if (serviceConnectionType == ServiceConnectionType.PollingOverWebSocket)
            {
                using var tcpPortConflictLock = await TcpPortHelper.WaitForLock(cancellationToken);
                var webSocketListeningPort = TcpPortHelper.FindFreeTcpPort();
                var webSocketPath = Guid.NewGuid().ToString();
                var webSocketListeningUrl = $"https://+:{webSocketListeningPort}/{webSocketPath}";
                var webSocketSslCertificateBindingAddress = $"0.0.0.0:{webSocketListeningPort}";

                octopus.ListenWebSocket(webSocketListeningUrl);
                tcpPortConflictLock.Dispose();

                portForwarder = portForwarderFactory?.Invoke(webSocketListeningPort);

                var webSocketSslCertificate = new WebSocketSslCertificateBuilder(webSocketSslCertificateBindingAddress).Build();
                disposableCollection.Add(webSocketSslCertificate);

                serviceUri = new Uri("poll://SQ-TENTAPOLL");

                if (portForwarder != null)
                {
                    webSocketListeningPort = portForwarder.ListeningPort;
                }

                if (hasService)
                {
                    var webSocketServiceEndpointUri = new Uri($"wss://localhost:{webSocketListeningPort}/{webSocketPath}");
                    runningOldHalibutBinary = await new HalibutTestBinaryRunner(
                        serviceConnectionType,
                        webSocketServiceEndpointUri,
                        clientCertAndThumbprint,
                        serviceCertAndThumbprint,
                        version,
                        proxyDetails,
                        halibutLogLevel,
                        availableServices).Run();
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
                        version,
                        proxyDetails,
                        halibutLogLevel,
                        availableServices).Run();

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

            return new ClientAndService(octopus, runningOldHalibutBinary, serviceUri, serviceCertAndThumbprint, portForwarder, disposableCollection, proxy, proxyDetails, cancellationTokenSource);
        }

        public class ClientAndService : IClientAndService
        {
            readonly HalibutTestBinaryRunner.RunningOldHalibutBinary? runningOldHalibutBinary;
            readonly Uri serviceUri;
            readonly CertAndThumbprint serviceCertAndThumbprint; // for creating a client
            readonly DisposableCollection disposableCollection;
            readonly ProxyDetails? proxyDetails;
            readonly CancellationTokenSource cancellationTokenSource;

            public ClientAndService(HalibutRuntime client,
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
                PortForwarder = portForwarder;
                HttpProxy = httpProxy;
                this.disposableCollection = disposableCollection;
                this.proxyDetails = proxyDetails;
                this.cancellationTokenSource = cancellationTokenSource;
            }

            public HalibutRuntime Client { get; }
            public PortForwarder? PortForwarder { get; }
            public HttpProxyService? HttpProxy { get; }

            public TService CreateClient<TService>(CancellationToken? cancellationToken = null, string? remoteThumbprint = null)
            {
                return CreateClient<TService>(s => { }, cancellationToken ?? CancellationToken.None, remoteThumbprint);
            }

            public TService CreateClient<TService>(Action<ServiceEndPoint> modifyServiceEndpoint)
            {
                return CreateClient<TService>(modifyServiceEndpoint, CancellationToken.None);
            }

            public TService CreateClient<TService>(Action<ServiceEndPoint> modifyServiceEndpoint, CancellationToken cancellationToken, string? remoteThumbprint = null)
            {
                var serviceEndpoint = new ServiceEndPoint(serviceUri, remoteThumbprint ?? serviceCertAndThumbprint.Thumbprint, proxyDetails);
                modifyServiceEndpoint(serviceEndpoint);
                return Client.CreateClient<TService>(serviceEndpoint, cancellationToken);
            }

            public TClientService CreateClient<TService, TClientService>()
            {
                return CreateClient<TService, TClientService>(_ => { });
            }

            public TClientService CreateClient<TService, TClientService>(Action<ServiceEndPoint> modifyServiceEndpoint)
            {
                var serviceEndpoint = new ServiceEndPoint(serviceUri, serviceCertAndThumbprint.Thumbprint, proxyDetails);
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
                Try.CatchingError(() => runningOldHalibutBinary?.Dispose(), logError);
                Try.CatchingError(() => HttpProxy?.Dispose(), logError);
                Try.CatchingError(() => PortForwarder?.Dispose(), logError);
                Try.CatchingError(() => disposableCollection.Dispose(), logError);
                Try.CatchingError(() => cancellationTokenSource?.Dispose(), logError);
            }
        }
    }
}
