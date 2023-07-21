using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Logging;
using Halibut.ServiceModel;
using Halibut.TestProxy;
using Halibut.TestUtils.Contracts;
using Halibut.TestUtils.Contracts.Tentacle.Services;
using Halibut.Transport.Proxy;
using Octopus.Tentacle.Contracts;
using Octopus.Tentacle.Contracts.Capabilities;
using Octopus.Tentacle.Contracts.ScriptServiceV2;
using Octopus.TestPortForwarder;
using Serilog.Extensions.Logging;

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
        readonly CertAndThumbprint serviceCertAndThumbprint;
        readonly CertAndThumbprint clientCertAndThumbprint = CertAndThumbprint.Octopus;
        string? version = null;
        Func<HttpProxyService>? proxyFactory;
        IEchoService echoService = new EchoService();
        ICachingService cachingService = new CachingService();
        IMultipleParametersTestService multipleParametersTestService = new MultipleParametersTestService();
        IComplexObjectService complexObjectService = new ComplexObjectService();
        Func<int, PortForwarder>? portForwarderFactory;
        LogLevel halibutLogLevel;
        bool withTentacleServices = false;
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
            this.version = version.ToString();
            return this;
        }

        public PreviousClientVersionAndLatestServiceBuilder WithEchoServiceService(IEchoService echoService)
        {
            this.echoService = echoService;
            return this;
        }

        IClientAndServiceBuilder IClientAndServiceBuilder.WithCachingService()
        {
            throw new Exception("Caching service is not supported, when testing on the old Client. Since the old client is on external CLR which does not have the new caching attributes which this service is used to test.");
        }

        public PreviousClientVersionAndLatestServiceBuilder WithMultipleParametersTestService(IMultipleParametersTestService multipleParametersTestService)
        {
            this.multipleParametersTestService = multipleParametersTestService;
            return this;
        }

        public PreviousClientVersionAndLatestServiceBuilder WithComplexObjectService(IComplexObjectService complexObjectService)
        {
            this.complexObjectService = complexObjectService;
            return this;
        }
        
        public PreviousClientVersionAndLatestServiceBuilder WithLockService(ILockService lockService)
        {
            this.lockService = lockService;
            return this;
        }
        
        public PreviousClientVersionAndLatestServiceBuilder WithCountingService(ICountingService countingService)
        {
            this.countingService = countingService;
            return this;
        }

        IClientAndServiceBuilder IClientAndServiceBuilder.WithStandardServices()
        {
            return WithStandardServices();
        }

        public PreviousClientVersionAndLatestServiceBuilder WithStandardServices()
        {
            return WithEchoServiceService(new EchoService())
                .WithMultipleParametersTestService(new MultipleParametersTestService())
                .WithComplexObjectService(new ComplexObjectService())
                .WithLockService(new LockService())
                .WithCountingService(new CountingService());
        }

        IClientAndServiceBuilder IClientAndServiceBuilder.WithTentacleServices()
        {
            return WithTenancyServices();
        }

        public PreviousClientVersionAndLatestServiceBuilder WithTenancyServices()
        {
            withTentacleServices = true;

            return this;
        }

        IClientAndServiceBuilder IClientAndServiceBuilder.WithProxy()
        {
            return WithProxy();
        }

        public PreviousClientVersionAndLatestServiceBuilder WithProxy()
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
            CancellationTokenSource cancellationTokenSource = new();
            if (version == null)
            {
                throw new Exception("The version of the service must be set.");
            }

            // A Halibut Runtime that is used as a client to forward requests to the previous version
            // of Halibut Runtime that will actually talk to the Service (Tentacle) below
            var proxyClient = new HalibutRuntime(clientCertAndThumbprint.Certificate2);
            proxyClient.Trust(serviceCertAndThumbprint.Thumbprint);

            var serviceFactory = new DelegateServiceFactory()
                .Register(() => echoService)
                .Register(() => cachingService)
                .Register(() => multipleParametersTestService)
                .Register(() => complexObjectService)
                .Register(() => lockService)
                .Register(() => countingService);

            if (withTentacleServices)
            {
                serviceFactory.Register<IFileTransferService>(() => new FileTransferService());
                serviceFactory.Register<IScriptService>(() => new ScriptService());
                serviceFactory.Register<IScriptServiceV2>(() => new ScriptServiceV2());
                serviceFactory.Register<ICapabilitiesServiceV2>(() => new CapabilitiesServiceV2());
            }

            // The Halibut Runtime that will be used as the Service (Tentacle)
            var service = new HalibutRuntimeBuilder()
                .WithServiceFactory(serviceFactory)
                .WithServerCertificate(serviceCertAndThumbprint.Certificate2)
                .WithLogFactory(new TestContextLogFactory("Tentacle", halibutLogLevel))
                .Build();

            PortForwarder? portForwarder = null;
            var disposableCollection = new DisposableCollection();
            ProxyHalibutTestBinaryRunner.RoundTripRunningOldHalibutBinary runningOldHalibutBinary;
            Uri serviceUri;
            var httpProxy = proxyFactory?.Invoke();
            ProxyDetails? httpProxyDetails = null;

            if (httpProxy != null)
            {
                await httpProxy.StartAsync(cancellationTokenSource.Token);
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
                    version,
                    httpProxyDetails,
                    null,
                    halibutLogLevel).Run();

                serviceUri = new Uri("poll://SQ-TENTAPOLL");

                portForwarder = portForwarderFactory?.Invoke((int) runningOldHalibutBinary.ProxyClientListenPort!);

                var listenPort = portForwarder?.ListeningPort ?? (int)runningOldHalibutBinary.ProxyClientListenPort!;
                service.Poll(serviceUri, new ServiceEndPoint(new Uri("https://localhost:" + listenPort), clientCertAndThumbprint.Thumbprint, httpProxyDetails));
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
                    version,
                    httpProxyDetails,
                    webSocketPath,
                    halibutLogLevel).Run();

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

                service.Poll(serviceUri, new ServiceEndPoint(webSocketServiceEndpointUri, Certificates.SslThumbprint, httpProxyDetails));
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
                    version,
                    httpProxyDetails,
                    null,
                    halibutLogLevel).Run();

                serviceUri = new Uri("https://localhost:" + runningOldHalibutBinary.ServiceListenPort);
            }

            return new ClientAndService(proxyClient, runningOldHalibutBinary, serviceUri, serviceCertAndThumbprint, service, disposableCollection, cancellationTokenSource, portForwarder, httpProxy);
        }

        public class ClientAndService : IClientAndService
        {
            readonly ProxyHalibutTestBinaryRunner.RoundTripRunningOldHalibutBinary runningOldHalibutBinary;
            readonly Uri serviceUri;
            readonly CertAndThumbprint serviceCertAndThumbprint; // for creating a client
            readonly HalibutRuntime service;
            readonly DisposableCollection disposableCollection;
            readonly CancellationTokenSource cancellationTokenSource;

            public ClientAndService(HalibutRuntime proxyClient,
                ProxyHalibutTestBinaryRunner.RoundTripRunningOldHalibutBinary runningOldHalibutBinary,
                Uri serviceUri,
                CertAndThumbprint serviceCertAndThumbprint,
                HalibutRuntime service,
                DisposableCollection disposableCollection,
                CancellationTokenSource cancellationTokenSource,
                PortForwarder? portForwarder,
                HttpProxyService? httpProxy)
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
            }

            /// <summary>
            /// This is the ProxyClient
            /// </summary>
            public HalibutRuntime Client { get; }
            public HalibutRuntime ProxyClient => Client;
            public PortForwarder? PortForwarder { get; }
            public HttpProxyService? HttpProxy { get; }

            /// <summary>
            /// Creates a client, which forwards RPC calls on to a proxy in a external process which will make those calls back
            /// to service in the this process.
            /// </summary>
            /// <typeparam name="TService"></typeparam>
            /// <returns></returns>
            public TService CreateClient<TService>(CancellationToken? cancellationToken = null, string? remoteThumbprint = null)
            {
                if (remoteThumbprint == null) remoteThumbprint = serviceCertAndThumbprint.Thumbprint;
                if (remoteThumbprint != serviceCertAndThumbprint.Thumbprint)
                {
                    throw new Exception("Setting the remote thumbprint is unsupported, since it would not actually be passed on to the remote process which holds the actual client under test.");
                }

                if (cancellationToken != null && cancellationToken != CancellationToken.None)
                {
                    throw new Exception("Setting the connect cancellation token to anything other than none is unsupported, since it would not actually be passed on to the remote process which holds the actual client under test.");
                }

                var serviceEndpoint = new ServiceEndPoint(serviceUri, serviceCertAndThumbprint.Thumbprint);
                return ProxyClient.CreateClient<TService>(serviceEndpoint, cancellationToken??CancellationToken.None);
            }

            /// <summary>
            /// This probably never makes sense to be called, since this modifies the client to the proxy client. If a test
            /// wanted to set these it is probably setting them on the wrong client
            /// </summary>
            /// <param name="modifyServiceEndpoint"></param>
            /// <param name="cancellationToken"></param>
            /// <param name="remoteThumbprint"></param>
            /// <typeparam name="TService"></typeparam>
            /// <returns></returns>
            public TService CreateClient<TService>(Action<ServiceEndPoint> modifyServiceEndpoint, CancellationToken cancellationToken, string? remoteThumbprint = null)
            {
                throw new Exception("Modifying the service endpoint is unsupported, since it would not actually be passed on to the remote process which holds the actual client under test.");
            }

            public TService CreateClient<TService>(Action<ServiceEndPoint> modifyServiceEndpoint)
            {
                return CreateClient<TService>(s => { }, CancellationToken.None);
            }

            public TClientService CreateClient<TService, TClientService>()
            {
                return CreateClient<TService, TClientService>(s => { });
            }

            public TClientService CreateClient<TService, TClientService>(Action<ServiceEndPoint> modifyServiceEndpoint)
            {
                throw new Exception($"Unsupported, since the {typeof(TClientService)} would not actually be passed on to the remote process which holds the actual client under test.");
            }

            public void Dispose()
            {
                var logger = new SerilogLoggerBuilder().Build().ForContext<ClientAndService>();
                logger.Information("Dispose called");
                Action<Exception> logError = e => logger.Warning(e, "Ignoring error in dispose");
                
                Try.CatchingError(() => cancellationTokenSource?.Cancel(), logError);
                Try.CatchingError(Client.Dispose, logError);
                Try.CatchingError(runningOldHalibutBinary.Dispose, logError);
                Try.CatchingError(service.Dispose, logError);
                Try.CatchingError(() => HttpProxy?.Dispose(), logError);
                Try.CatchingError(() => PortForwarder?.Dispose(), logError);
                Try.CatchingError(disposableCollection.Dispose, logError); ;
                Try.CatchingError(() => cancellationTokenSource?.Dispose(), logError);
            }
        }
    }
}
