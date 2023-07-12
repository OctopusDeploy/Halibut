#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Logging;
using Halibut.ServiceModel;
using Halibut.TestProxy;
using Halibut.Tests.TestServices;
using Halibut.Transport.Proxy;
using Octopus.TestPortForwarder;
using Serilog.Extensions.Logging;

namespace Halibut.Tests.Support.BackwardsCompatibility
{
    /// <summary>
    /// Used to test old versions of a client talking to a new version of service.
    /// In this case the client is run out of process, and talks to a service running
    /// in this process.
    /// </summary>
    public class PreviousClientVersionAndServiceBuilder: IClientAndServiceBaseBuilder
    {
        readonly ServiceConnectionType serviceConnectionType;
        readonly CertAndThumbprint serviceCertAndThumbprint;
        readonly CertAndThumbprint clientCertAndThumbprint = CertAndThumbprint.Octopus;
        string? version = null;
        Func<HttpProxyService>? proxyFactory;
        CancellationTokenSource cancellationTokenSource = new();
        IEchoService echoService = new EchoService();
        ICachingService cachingService = new CachingService();
        IMultipleParametersTestService multipleParametersTestService = new MultipleParametersTestService();
        Func<int, PortForwarder>? portForwarderFactory;
        LogLevel halibutLogLevel;

        PreviousClientVersionAndServiceBuilder(ServiceConnectionType serviceConnectionType, CertAndThumbprint serviceCertAndThumbprint)
        {
            this.serviceConnectionType = serviceConnectionType;
            this.serviceCertAndThumbprint = serviceCertAndThumbprint;
        }

        public static PreviousClientVersionAndServiceBuilder WithPollingService()
        {
            return new PreviousClientVersionAndServiceBuilder(ServiceConnectionType.Polling, CertAndThumbprint.TentaclePolling);
        }

        public static PreviousClientVersionAndServiceBuilder WithListeningService()
        {
            return new PreviousClientVersionAndServiceBuilder(ServiceConnectionType.Listening, CertAndThumbprint.TentacleListening);
        }

        public static PreviousClientVersionAndServiceBuilder ForServiceConnectionType(ServiceConnectionType connectionType)
        {
            switch (connectionType)
            {
                case ServiceConnectionType.Polling:
                    return WithPollingService();
                case ServiceConnectionType.Listening:
                    return WithListeningService();
                default:
                    throw new ArgumentOutOfRangeException(nameof(connectionType), connectionType, null);
            }
        }

        IClientAndServiceBaseBuilder IClientAndServiceBaseBuilder.WithPortForwarding(Func<int, PortForwarder> func)
        {
            return WithPortForwarding(func);
        }

        public PreviousClientVersionAndServiceBuilder WithPortForwarding(Func<int, PortForwarder> portForwarderFactory)
        {
            this.portForwarderFactory = portForwarderFactory;
            return this;
        }

        public PreviousClientVersionAndServiceBuilder WithClientVersion(string version)
        {
            this.version = version;
            return this;
        }

        public PreviousClientVersionAndServiceBuilder WithEchoServiceService(IEchoService echoService)
        {
            this.echoService = echoService;
            return this;
        }

        public PreviousClientVersionAndServiceBuilder WithCachingService(ICachingService cachingService)
        {
            this.cachingService = cachingService;
            return this;
        }

        public PreviousClientVersionAndServiceBuilder WithMultipleParametersTestService(IMultipleParametersTestService multipleParametersTestService)
        {
            this.multipleParametersTestService = multipleParametersTestService;
            return this;
        }
        
        public PreviousClientVersionAndServiceBuilder WithProxy()
        {
            this.proxyFactory = () =>
            {
                var options = new HttpProxyOptions();
                var loggerFactory = new SerilogLoggerFactory(new SerilogLoggerBuilder().Build());

                return new HttpProxyService(options, loggerFactory);
            };

            return this;
        }
                
        IClientAndServiceBaseBuilder IClientAndServiceBaseBuilder.WithStandardServices()
        {
            return WithStandardServices();
        }

        public PreviousClientVersionAndServiceBuilder WithStandardServices()
        {
            return WithEchoServiceService(new EchoService()).WithCachingService(new CachingService()).WithMultipleParametersTestService(new MultipleParametersTestService());
        }

        public PreviousClientVersionAndServiceBuilder WithHalibutLoggingLevel(LogLevel halibutLogLevel)
        {
            this.halibutLogLevel = halibutLogLevel;

            return this;
        }

        async Task<IClientAndService> IClientAndServiceBaseBuilder.Build()
        {
            return await Build();
        }

        public async Task<ClientAndService> Build()
        {
            if (version == null)
            {
                throw new Exception("The version of the service must be set.");
            }

            // A Halibut Runtime that is used as a client to forward requests to the previous version
            // of Halibut Runtime that will actually talk to the Service (Tentacle) below
            var client = new HalibutRuntime(clientCertAndThumbprint.Certificate2);
            client.Trust(serviceCertAndThumbprint.Thumbprint);

            // The Halibut Runtime that will be used as the Service (Tentacle)
            var service = new HalibutRuntimeBuilder()
                .WithServiceFactory(new DelegateServiceFactory()
                    .Register(() => echoService)
                    .Register(() => cachingService)
                    .Register(() => multipleParametersTestService))
                .WithServerCertificate(serviceCertAndThumbprint.Certificate2)
                .WithLogFactory(new TestContextLogFactory("Tentacle", halibutLogLevel))
                .Build();

            PortForwarder? portForwarder = null;
            var disposableCollection = new DisposableCollection();
            ProxyHalibutTestBinaryRunner.RoundTripRunningOldHalibutBinary runningOldHalibutBinary;
            Uri proxyServiceUri;
            var proxy = proxyFactory?.Invoke();
            ProxyDetails? proxyDetails = null;

            if (proxy != null)
            {
                await proxy.StartAsync(cancellationTokenSource.Token);
                proxyDetails = new ProxyDetails("localhost", proxy.Endpoint.Port, ProxyType.HTTP);
                disposableCollection.Add(proxy);
            }

            if (serviceConnectionType == ServiceConnectionType.Polling)
            {
                var clientListenPort = client.Listen();

                runningOldHalibutBinary = await new ProxyHalibutTestBinaryRunner(
                    serviceConnectionType,
                    clientListenPort,
                    clientCertAndThumbprint,
                    serviceCertAndThumbprint,
                    new Uri("poll://SQ-TENTAPOLL"),
                    version,
                    proxyDetails,
                    halibutLogLevel).Run();

                proxyServiceUri = new Uri("poll://SQ-TENTAPOLL");

                portForwarder = portForwarderFactory?.Invoke((int) runningOldHalibutBinary.ProxyClientListenPort!);

                var listenPort = portForwarder?.ListeningPort ?? (int)runningOldHalibutBinary.ProxyClientListenPort!;
                service.Poll(proxyServiceUri, new ServiceEndPoint(new Uri("https://localhost:" + listenPort), clientCertAndThumbprint.Thumbprint, proxyDetails));
            }
            else
            {
                var listenPort = service.Listen();
                service.Trust(clientCertAndThumbprint.Thumbprint);
                portForwarder = portForwarderFactory != null ? portForwarderFactory(listenPort) : null;
                listenPort = portForwarder?.ListeningPort??listenPort;
                runningOldHalibutBinary = await new ProxyHalibutTestBinaryRunner(
                    serviceConnectionType,
                    null,
                    clientCertAndThumbprint,
                    serviceCertAndThumbprint,
                    new Uri("https://localhost:" + listenPort),
                    version,
                    proxyDetails,
                    halibutLogLevel).Run();

                proxyServiceUri = new Uri("https://localhost:" + runningOldHalibutBinary.ServiceListenPort);
            }

            return new ClientAndService(client, runningOldHalibutBinary, proxyServiceUri, serviceCertAndThumbprint, service, disposableCollection, cancellationTokenSource, portForwarder, proxy);
        }

        public class ClientAndService : IClientAndService
        {
            readonly ProxyHalibutTestBinaryRunner.RoundTripRunningOldHalibutBinary runningOldHalibutBinary;
            readonly Uri serviceUri;
            readonly CertAndThumbprint serviceCertAndThumbprint; // for creating a client
            readonly HalibutRuntime tentacle;
            readonly DisposableCollection disposableCollection;
            readonly CancellationTokenSource cancellationTokenSource;

            public ClientAndService(HalibutRuntime octopus,
                ProxyHalibutTestBinaryRunner.RoundTripRunningOldHalibutBinary runningOldHalibutBinary,
                Uri serviceUri,
                CertAndThumbprint serviceCertAndThumbprint,
                HalibutRuntime tentacle,
                DisposableCollection disposableCollection,
                CancellationTokenSource cancellationTokenSource,
                PortForwarder? portForwarder,
                HttpProxyService? proxy)
            {
                Octopus = octopus;
                Proxy = proxy;
                PortForwarder = portForwarder;
                this.runningOldHalibutBinary = runningOldHalibutBinary;
                this.serviceUri = serviceUri;
                this.serviceCertAndThumbprint = serviceCertAndThumbprint;
                this.tentacle = tentacle;
                this.disposableCollection = disposableCollection;
                this.cancellationTokenSource = cancellationTokenSource;
            }

            public HalibutRuntime Octopus { get; }
            public PortForwarder? PortForwarder { get; }
            public HttpProxyService? Proxy { get; }

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
                return Octopus.CreateClient<TService>(serviceEndpoint, cancellationToken??CancellationToken.None);
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
                cancellationTokenSource?.Cancel();
                Octopus.Dispose();
                runningOldHalibutBinary.Dispose();
                tentacle.Dispose();
                disposableCollection.Dispose();
                Proxy?.Dispose();
                PortForwarder?.Dispose();
                cancellationTokenSource?.Dispose();
            }
        }
    }
}
