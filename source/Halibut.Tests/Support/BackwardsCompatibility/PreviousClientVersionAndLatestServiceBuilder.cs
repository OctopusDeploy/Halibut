#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics.LogCreators;
using Halibut.Logging;
using Halibut.ServiceModel;
using Halibut.TestProxy;
using Halibut.Tests.Builders;
using Halibut.Tests.Support.Logging;
using Halibut.Tests.TestServices;
using Halibut.TestUtils.Contracts;
using Halibut.TestUtils.Contracts.Tentacle.Services;
using Halibut.Transport.Proxy;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Capabilities;
using Octopus.Tentacle.Contracts.ScriptServiceV2;
using Octopus.TestPortForwarder;
using Serilog;
using ICachingService = Halibut.TestUtils.Contracts.ICachingService;

namespace Halibut.Tests.Support.BackwardsCompatibility
{
    /// <summary>
    /// Used to test old versions of a client talking to the latest version of a service.
    /// In this case the client is run out of process, and talks to a service running
    /// in this process.
    /// </summary>
    public class PreviousClientVersionAndLatestServiceBuilder: IClientAndServiceBuilder
    {
        readonly ServiceConnectionType serviceConnectionType;
        
        ServiceFactoryBuilder serviceFactoryBuilder = new();
        IServiceFactory? serviceFactory;
        readonly CertAndThumbprint serviceCertAndThumbprint;
        readonly CertAndThumbprint clientCertAndThumbprint = CertAndThumbprint.Octopus;
        Version? version = null;
        ProxyFactory? proxyFactory;
        Func<int, PortForwarder>? portForwarderFactory;
        LogLevel halibutLogLevel = LogLevel.Trace;
        ILockService lockService;
        ICountingService countingService;

        PreviousClientVersionAndLatestServiceBuilder(ServiceConnectionType serviceConnectionType, CertAndThumbprint serviceCertAndThumbprint)
        {
            this.serviceConnectionType = serviceConnectionType;
            this.serviceCertAndThumbprint = serviceCertAndThumbprint;
        }

        public static PreviousClientVersionAndLatestServiceBuilder WithPollingService()
        {
            return new PreviousClientVersionAndLatestServiceBuilder(ServiceConnectionType.Polling, CertAndThumbprint.TentaclePolling);
        }

        public static PreviousClientVersionAndLatestServiceBuilder WithPollingOverWebSocketsService()
        {
            return new PreviousClientVersionAndLatestServiceBuilder(ServiceConnectionType.PollingOverWebSocket, CertAndThumbprint.TentaclePolling);
        }

        public static PreviousClientVersionAndLatestServiceBuilder WithListeningService()
        {
            return new PreviousClientVersionAndLatestServiceBuilder(ServiceConnectionType.Listening, CertAndThumbprint.TentacleListening);
        }

        public static PreviousClientVersionAndLatestServiceBuilder ForServiceConnectionType(ServiceConnectionType connectionType)
        {
            switch (connectionType)
            {
                case ServiceConnectionType.Polling:
                    return WithPollingService();
                case ServiceConnectionType.PollingOverWebSocket:
                    return WithPollingOverWebSocketsService();
                case ServiceConnectionType.Listening:
                    return WithListeningService();
                default:
                    throw new ArgumentOutOfRangeException(nameof(connectionType), connectionType, null);
            }
        }

        IClientAndServiceBuilder IClientAndServiceBuilder.WithPortForwarding(Func<int, PortForwarder> portForwarderFactory)
        {
            return WithPortForwarding(portForwarderFactory);
        }

        public PreviousClientVersionAndLatestServiceBuilder WithPortForwarding(Func<int, PortForwarder> portForwarderFactory)
        {
            if (this.portForwarderFactory != null)
            {
                throw new NotSupportedException("A PortForwarderFactory is already registered with the Builder. Only one PortForwarder is supported");
            }

            this.portForwarderFactory = portForwarderFactory;
            return this;
        }

        public PreviousClientVersionAndLatestServiceBuilder WithClientVersion(Version version)
        {
            this.version = version;
            return this;
        }

        public PreviousClientVersionAndLatestServiceBuilder WithAsyncService<TContract, TClientContract>(Func<TClientContract> implementation)
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

        IClientAndServiceBuilder IClientAndServiceBuilder.WithCachingService()
        {
            throw new Exception("Caching service is not supported, when testing on the old Client. Since the old client is on external CLR which does not have the new caching attributes which this service is used to test.");
        }

        IClientAndServiceBuilder IClientAndServiceBuilder.WithStandardServices()
        {
            return WithStandardServices();
        }

        public PreviousClientVersionAndLatestServiceBuilder WithStandardServices()
        {
            return this
                .WithEchoService()
                .WithMultipleParametersTestService()
                .WithComplexObjectService()
                .WithLockService()
                .WithCountingService()
                .WithReadDataStreamService();
        }

        IClientAndServiceBuilder IClientAndServiceBuilder.WithTentacleServices()
        {
            return WithTentacleServices();
        }

        public PreviousClientVersionAndLatestServiceBuilder WithTentacleServices()
        {
            return this
                .WithAsyncService<IFileTransferService, IAsyncFileTransferService>(() => new AsyncFileTransferService())
                .WithAsyncService<IScriptService, IAsyncScriptService>(() => new AsyncScriptService())
                .WithAsyncService<IScriptServiceV2, IAsyncScriptServiceV2>(() => new AsyncScriptServiceV2())
                .WithAsyncService<ICapabilitiesServiceV2, IAsyncCapabilitiesServiceV2>(() => new AsyncCapabilitiesServiceV2());
        }

        IClientAndServiceBuilder IClientAndServiceBuilder.WithProxy()
        {
            return WithProxy();
        }

        public PreviousClientVersionAndLatestServiceBuilder WithProxy()
        {
            this.proxyFactory = new ProxyFactory().WithDelaySendingSectionsOfHttpHeaders(false);

            return this;
        }
        
        IClientAndServiceBuilder IClientAndServiceBuilder.WithHalibutLoggingLevel(LogLevel halibutLogLevel)
        {
            return WithHalibutLoggingLevel(halibutLogLevel);
        }

        public PreviousClientVersionAndLatestServiceBuilder WithHalibutLoggingLevel(LogLevel halibutLogLevel)
        {
            this.halibutLogLevel = halibutLogLevel;

            return this;
        }

        async Task<IClientAndService> IClientAndServiceBuilder.Build(CancellationToken cancellationToken)
        {
            return await Build(cancellationToken);
        }
        
        public PreviousClientVersionAndLatestServiceBuilder NoService()
        {
            throw new NotImplementedException();
        }

        IClientAndServiceBuilder IClientAndServiceBuilder.NoService()
        {
            return NoService();
        }
        
        public async Task<ClientAndService> Build(CancellationToken cancellationToken)
        {
            var logger = new SerilogLoggerBuilder().Build().ForContext<PreviousClientVersionAndLatestServiceBuilder>();
            CancellationTokenSource cancellationTokenSource = new();
            if (version == null)
            {
                throw new Exception("The version of the service must be set.");
            }

            // A Halibut Runtime that is used as a client to forward requests to the previous version
            // of Halibut Runtime that will actually talk to the Service (Tentacle) below
            var proxyClient = new HalibutRuntimeBuilder()
                .WithServerCertificate(clientCertAndThumbprint.Certificate2)
                .WithLogFactory(new TestContextLogCreator("ProxyClient", halibutLogLevel).ToCachingLogFactory())
                .WithHalibutTimeoutsAndLimits(new HalibutTimeoutsAndLimitsForTestsBuilder().Build())
                .Build();
            
            proxyClient.Trust(serviceCertAndThumbprint.Thumbprint);
            
            var serviceFactory = serviceFactoryBuilder.Build();

            // The Halibut Runtime that will be used as the Service (Tentacle)
            var service = new HalibutRuntimeBuilder()
                .WithServiceFactory(serviceFactory)
                .WithServerCertificate(serviceCertAndThumbprint.Certificate2)
                .WithLogFactory(new TestContextLogCreator("Tentacle", halibutLogLevel).ToCachingLogFactory())
                .WithHalibutTimeoutsAndLimits(new HalibutTimeoutsAndLimitsForTestsBuilder().Build())
                .Build();

            PortForwarder? portForwarder = null;
            var disposableCollection = new DisposableCollection();
            ProxyHalibutTestBinaryRunner.RoundTripRunningOldHalibutBinary runningOldHalibutBinary;
            Uri serviceUri;
            var httpProxy = proxyFactory?.Build();
            ProxyDetails? httpProxyDetails = null;

            if (httpProxy != null)
            {
                await httpProxy.StartAsync();
                httpProxyDetails = new ProxyDetails("localhost", httpProxy.Endpoint!.Port, ProxyType.HTTP);
            }

            if (serviceConnectionType == ServiceConnectionType.Polling)
            {
                var proxyClientListeningPort = proxyClient.Listen();

                runningOldHalibutBinary = await new ProxyHalibutTestBinaryRunner(
                    serviceConnectionType,
                    proxyClientListeningPort,
                    clientCertAndThumbprint,
                    serviceCertAndThumbprint,
                    new Uri("poll://SQ-TENTAPOLL"),
                    version?.ToString(),
                    httpProxyDetails,
                    null,
                    halibutLogLevel,
                    logger).Run();

                serviceUri = new Uri("poll://SQ-TENTAPOLL");

                portForwarder = portForwarderFactory?.Invoke((int) runningOldHalibutBinary.ProxyClientListenPort!);

                var listenPort = portForwarder?.ListeningPort ?? (int)runningOldHalibutBinary.ProxyClientListenPort!;
                service.Poll(
                    serviceUri,
                    new ServiceEndPoint(new Uri("https://localhost:" + listenPort), clientCertAndThumbprint.Thumbprint, httpProxyDetails, service.TimeoutsAndLimits),
                    CancellationToken.None);
            }
            else if (serviceConnectionType == ServiceConnectionType.PollingOverWebSocket)
            {
                // For simplicity the proxy client Listens rather than Listens over WebSockets
                // The real client will Listen over WebSockets
                var proxyClientListeningPort = proxyClient.Listen();
                var webSocketPath = Guid.NewGuid().ToString();

                runningOldHalibutBinary = await new ProxyHalibutTestBinaryRunner(
                    serviceConnectionType,
                    proxyClientListeningPort,
                    clientCertAndThumbprint,
                    serviceCertAndThumbprint,
                    new Uri("poll://SQ-TENTAPOLL"),
                    version?.ToString(),
                    httpProxyDetails,
                    webSocketPath,
                    halibutLogLevel,
                    logger).Run();

                serviceUri = new Uri("poll://SQ-TENTAPOLL");

                var webSocketListeningPort = runningOldHalibutBinary.ProxyClientListenPort!.Value;
                var webSocketSslCertificateBindingAddress = $"0.0.0.0:{webSocketListeningPort}";
                var webSocketSslCertificate = new WebSocketSslCertificateBuilder(webSocketSslCertificateBindingAddress).Build();
                disposableCollection.Add(webSocketSslCertificate);

                portForwarder = portForwarderFactory?.Invoke((int)runningOldHalibutBinary.ProxyClientListenPort!);

                if (portForwarder != null)
                {
                    webSocketListeningPort = (int)portForwarder?.ListeningPort!;
                }

                var webSocketServiceEndpointUri = new Uri($"wss://localhost:{webSocketListeningPort}/{webSocketPath}");

                service.Poll(
                    serviceUri,
                    new ServiceEndPoint(webSocketServiceEndpointUri, Certificates.SslThumbprint, httpProxyDetails, service.TimeoutsAndLimits),
                    CancellationToken.None);
            }
            else
            {
                var serviceListeningPort = service.Listen();
                service.Trust(clientCertAndThumbprint.Thumbprint);

                portForwarder = portForwarderFactory != null ? portForwarderFactory(serviceListeningPort) : null;
                serviceListeningPort = portForwarder?.ListeningPort ?? serviceListeningPort;

                runningOldHalibutBinary = await new ProxyHalibutTestBinaryRunner(
                    serviceConnectionType,
                    null,
                    clientCertAndThumbprint,
                    serviceCertAndThumbprint,
                    new Uri("https://localhost:" + serviceListeningPort),
                    version?.ToString(),
                    httpProxyDetails,
                    null,
                    halibutLogLevel,
                    logger).Run();

                serviceUri = new Uri("https://localhost:" + runningOldHalibutBinary.ServiceListenPort);
            }

            return new ClientAndService(proxyClient, runningOldHalibutBinary, serviceUri, serviceCertAndThumbprint, service, disposableCollection, cancellationTokenSource, portForwarder, httpProxy, logger);
        }

        public class ClientAndService : IClientAndService
        {
            readonly ProxyHalibutTestBinaryRunner.RoundTripRunningOldHalibutBinary runningOldHalibutBinary;
            readonly Uri serviceUri;
            readonly CertAndThumbprint serviceCertAndThumbprint; // for creating a client
            readonly HalibutRuntime service;
            readonly DisposableCollection disposableCollection;
            readonly CancellationTokenSource cancellationTokenSource;
            readonly ILogger logger;

            public ClientAndService(
                HalibutRuntime proxyClient,
                ProxyHalibutTestBinaryRunner.RoundTripRunningOldHalibutBinary runningOldHalibutBinary,
                Uri serviceUri,
                CertAndThumbprint serviceCertAndThumbprint,
                HalibutRuntime service,
                DisposableCollection disposableCollection,
                CancellationTokenSource cancellationTokenSource,
                PortForwarder? portForwarder,
                HttpProxyService? httpProxy,
                ILogger logger)
            {
                Client = proxyClient;
                HttpProxy = httpProxy;
                PortForwarder = portForwarder;
                this.runningOldHalibutBinary = runningOldHalibutBinary;
                this.serviceUri = serviceUri;
                this.serviceCertAndThumbprint = serviceCertAndThumbprint;
                this.service = service;
                this.disposableCollection = disposableCollection;
                this.cancellationTokenSource = cancellationTokenSource;
                this.logger = logger.ForContext<ClientAndService>();;
            }

            /// <summary>
            /// This is the ProxyClient
            /// </summary>
            public HalibutRuntime Client { get; }

            public ServiceEndPoint ServiceEndPoint => new ServiceEndPoint(serviceUri, serviceCertAndThumbprint.Thumbprint, Client.TimeoutsAndLimits);

            public HalibutRuntime ProxyClient => Client;
            public PortForwarder? PortForwarder { get; }
            public HttpProxyService? HttpProxy { get; }
            
            public TAsyncClientService CreateAsyncClient<TService, TAsyncClientService>(Action<ServiceEndPoint> modifyServiceEndpoint)
            {
                throw new NotSupportedException("Not supported since the options can not be passed to the external binary.");
            }
            
            public TAsyncClientService CreateAsyncClient<TService, TAsyncClientService>()
            {
                return Client.CreateAsyncClient<TService, TAsyncClientService>(ServiceEndPoint);
            }

            public async ValueTask DisposeAsync()
            {
                logger.Information("****** ****** ****** ****** ****** ****** ******");
                logger.Information("****** CLIENT AND SERVICE DISPOSE CALLED  ******");
                logger.Information("*     Subsequent errors should be ignored      *");
                logger.Information("****** ****** ****** ****** ****** ****** ******");

                void LogError(Exception e) => logger.Warning(e, "Ignoring error in dispose");

                Try.CatchingError(() => cancellationTokenSource.Cancel(), LogError);
                await Try.DisposingAsync(Client, LogError);
                Try.CatchingError(runningOldHalibutBinary.Dispose, LogError);
                await Try.DisposingAsync(service, LogError);
                Try.CatchingError(() => HttpProxy?.Dispose(), LogError);
                Try.CatchingError(() => PortForwarder?.Dispose(), LogError);
                Try.CatchingError(disposableCollection.Dispose, LogError); ;
                Try.CatchingError(() => cancellationTokenSource.Dispose(), LogError);
            }
        }
    }
}
