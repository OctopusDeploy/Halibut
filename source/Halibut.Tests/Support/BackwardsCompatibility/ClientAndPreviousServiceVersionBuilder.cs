#nullable enable
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
    public class ClientAndPreviousServiceVersionBuilder : IClientAndServiceBaseBuilder
    {
        readonly ServiceConnectionType serviceConnectionType;
        readonly CertAndThumbprint serviceCertAndThumbprint;
        readonly CertAndThumbprint clientCertAndThumbprint = CertAndThumbprint.Octopus;
        string? version = null;
        Func<int, PortForwarder> portForwarderFactory;
        Func<HttpProxyService>? proxyFactory;
        CancellationTokenSource cancellationTokenSource = new();
        LogLevel halibutLogLevel;

        ClientAndPreviousServiceVersionBuilder(ServiceConnectionType serviceConnectionType, CertAndThumbprint serviceCertAndThumbprint)
        {
            this.serviceConnectionType = serviceConnectionType;
            this.serviceCertAndThumbprint = serviceCertAndThumbprint;
        }

        public static ClientAndPreviousServiceVersionBuilder WithPollingService()
        {
            return new ClientAndPreviousServiceVersionBuilder(ServiceConnectionType.Polling, CertAndThumbprint.TentaclePolling);
        }

        public static ClientAndPreviousServiceVersionBuilder WithPollingOverWebSocketService()
        {
            return new ClientAndPreviousServiceVersionBuilder(ServiceConnectionType.PollingOverWebSocket, CertAndThumbprint.TentaclePolling);
        }

        public static ClientAndPreviousServiceVersionBuilder WithListeningService()
        {
            return new ClientAndPreviousServiceVersionBuilder(ServiceConnectionType.Listening, CertAndThumbprint.TentacleListening);
        }

        public static ClientAndPreviousServiceVersionBuilder ForServiceConnectionType(ServiceConnectionType connectionType)
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

        public ClientAndPreviousServiceVersionBuilder WithServiceVersion(string? version)
        {
            this.version = version;
            return this;
        }

        IClientAndServiceBaseBuilder IClientAndServiceBaseBuilder.WithPortForwarding(Func<int, PortForwarder> func)
        {
            return WithPortForwarding(func);
        }

        public ClientAndPreviousServiceVersionBuilder WithPortForwarding(Func<int, PortForwarder> portForwarderFactory)
        {
            this.portForwarderFactory = portForwarderFactory;
            return this;
        }

        IClientAndServiceBaseBuilder IClientAndServiceBaseBuilder.WithStandardServices()
        {
            return WithStandardServices();
        }

        public ClientAndPreviousServiceVersionBuilder WithStandardServices()
        {
            // TODO: EchoService is registered by default, so we don't need to do anything else here.
            // It would be better to be able to configure the Tentacle binary to register/not register
            // specific services, but we'll do that later.
            return this;
        }

        public ClientAndPreviousServiceVersionBuilder WithProxy()
        {
            this.proxyFactory = () =>
            {
                var options = new HttpProxyOptions();
                var loggerFactory = new SerilogLoggerFactory(new SerilogLoggerBuilder().Build());

                return new HttpProxyService(options, loggerFactory);
            };

            return this;
        }

        public ClientAndPreviousServiceVersionBuilder WithHalibutLoggingLevel(LogLevel halibutLogLevel)
        {
            this.halibutLogLevel = halibutLogLevel;

            return this;
        }

        public async Task<IClientAndService> Build()
        {
            if (version == null)
            {
                throw new Exception("The version of the service must be set.");
            }

            var octopus = new HalibutRuntime(clientCertAndThumbprint.Certificate2);
            octopus.Trust(serviceCertAndThumbprint.Thumbprint);

            HalibutTestBinaryRunner.RunningOldHalibutBinary runningOldHalibutBinary;
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

                runningOldHalibutBinary = await new HalibutTestBinaryRunner(
                    serviceConnectionType,
                    listenPort,
                    clientCertAndThumbprint,
                    serviceCertAndThumbprint,
                    version,
                    proxyDetails,
                    halibutLogLevel).Run();
            }
            else if (serviceConnectionType == ServiceConnectionType.PollingOverWebSocket)
            {
                var webSocketListeningPort = TcpPortHelper.FindFreeTcpPort();
                var webSocketPath = Guid.NewGuid().ToString();
                var webSocketListeningUrl = $"https://+:{webSocketListeningPort}/{webSocketPath}";
                var webSocketSslCertificateBindingAddress = $"0.0.0.0:{webSocketListeningPort}";

                octopus.ListenWebSocket(webSocketListeningUrl);

                var webSocketSslCertificate = new WebSocketSslCertificateBuilder(webSocketSslCertificateBindingAddress).Build();
                disposableCollection.Add(webSocketSslCertificate);

                serviceUri = new Uri("poll://SQ-TENTAPOLL");

                if (portForwarder != null)
                {
                    webSocketListeningPort = portForwarder.ListeningPort;
                }

                var webSocketServiceEndpointUri = new Uri($"wss://localhost:{webSocketListeningPort}/{webSocketPath}");
                runningOldHalibutBinary = await new HalibutTestBinaryRunner(
                    serviceConnectionType,
                    webSocketServiceEndpointUri,
                    clientCertAndThumbprint,
                    serviceCertAndThumbprint,
                    version,
                    proxyDetails,
                    halibutLogLevel).Run();
            }
            else if (serviceConnectionType == ServiceConnectionType.Listening)
            {
                runningOldHalibutBinary = await new HalibutTestBinaryRunner(
                    serviceConnectionType,
                    clientCertAndThumbprint,
                    serviceCertAndThumbprint,
                    version,
                    proxyDetails,
                    halibutLogLevel).Run();

                int listenPort = (int)runningOldHalibutBinary.ServiceListenPort!;
                if (portForwarder != null) listenPort = portForwarder.ListeningPort;
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
            readonly HalibutTestBinaryRunner.RunningOldHalibutBinary runningOldHalibutBinary;
            readonly Uri serviceUri;
            readonly CertAndThumbprint serviceCertAndThumbprint; // for creating a client
            readonly DisposableCollection disposableCollection;
            readonly ProxyDetails? proxyDetails;
            readonly CancellationTokenSource cancellationTokenSource;

            public ClientAndService(HalibutRuntime octopus,
                HalibutTestBinaryRunner.RunningOldHalibutBinary runningOldHalibutBinary,
                Uri serviceUri,
                CertAndThumbprint serviceCertAndThumbprint,
                PortForwarder? portForwarder,
                DisposableCollection disposableCollection,
                HttpProxyService? proxy,
                ProxyDetails? proxyDetails,
                CancellationTokenSource cancellationTokenSource)
            {
                Octopus = octopus;
                this.runningOldHalibutBinary = runningOldHalibutBinary;
                this.serviceUri = serviceUri;
                this.serviceCertAndThumbprint = serviceCertAndThumbprint;
                PortForwarder = portForwarder;
                Proxy = proxy;
                this.disposableCollection = disposableCollection;
                this.proxyDetails = proxyDetails;
                this.cancellationTokenSource = cancellationTokenSource;
            }

            public HalibutRuntime Octopus { get; }
            public PortForwarder? PortForwarder { get; }
            public HttpProxyService? Proxy { get; }

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
                return Octopus.CreateClient<TService>(serviceEndpoint, cancellationToken);
            }

            public TClientService CreateClient<TService, TClientService>()
            {
                return CreateClient<TService, TClientService>(_ => { });
            }

            public TClientService CreateClient<TService, TClientService>(Action<ServiceEndPoint> modifyServiceEndpoint)
            {
                var serviceEndpoint = new ServiceEndPoint(serviceUri, serviceCertAndThumbprint.Thumbprint, proxyDetails);
                modifyServiceEndpoint(serviceEndpoint);
                return Octopus.CreateClient<TService, TClientService>(serviceEndpoint);
            }

            public void Dispose()
            {
                cancellationTokenSource?.Cancel();
                Octopus.Dispose();
                Proxy?.Dispose();
                runningOldHalibutBinary.Dispose();
                disposableCollection.Dispose();
                cancellationTokenSource?.Dispose();
            }
        }
    }
}
