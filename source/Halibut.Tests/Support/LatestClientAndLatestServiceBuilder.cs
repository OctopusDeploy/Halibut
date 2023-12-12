using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.Logging;
using Halibut.ServiceModel;
using Halibut.TestProxy;
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

        readonly LatestClientBuilder clientBuilder;
        readonly LatestServiceBuilder serviceBuilder;

        ProxyFactory? proxyFactory;
        Reference<HttpProxyService>? proxyServiceReference;

        Reference<PortForwarder>? clientPortForwarderReference;
        Reference<PortForwarder>? servicePortForwarderReference;
        Reference<PortForwarder>? portForwarderReference;

        public LatestClientAndLatestServiceBuilder(
            ServiceConnectionType serviceConnectionType,
            CertAndThumbprint clientCertAndThumbprint,
            CertAndThumbprint serviceCertAndThumbprint)
        {
            ServiceConnectionType = serviceConnectionType;

            clientBuilder = new LatestClientBuilder(serviceConnectionType, clientCertAndThumbprint, serviceCertAndThumbprint);
            serviceBuilder = new LatestServiceBuilder(serviceConnectionType, clientCertAndThumbprint, serviceCertAndThumbprint);
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


        public LatestClientAndLatestServiceBuilder WithStreamFactory(IStreamFactory streamFactory)
        {
            clientBuilder.WithStreamFactory(streamFactory);
            serviceBuilder.WithStreamFactory(streamFactory);
            return this;
        }

        public LatestClientAndLatestServiceBuilder WithClientStreamFactory(IStreamFactory clientStreamFactory)
        {
            clientBuilder.WithStreamFactory(clientStreamFactory);
            return this;
        }

        public LatestClientAndLatestServiceBuilder WithServiceStreamFactory(IStreamFactory serviceStreamFactory)
        {
            serviceBuilder.WithStreamFactory(serviceStreamFactory);
            return this;
        }

        public LatestClientAndLatestServiceBuilder WithServiceConnectionsObserver(IConnectionsObserver connectionsObserver)
        {
            serviceBuilder.WithServiceConnectionsObserver(connectionsObserver);
            return this;
        }

        public LatestClientAndLatestServiceBuilder WithClientConnectionsObserver(IConnectionsObserver connectionsObserver)
        {
            clientBuilder.WithClientConnectionsObserver(connectionsObserver);
            return this;
        }

        public LatestClientAndLatestServiceBuilder WithHalibutTimeoutsAndLimits(HalibutTimeoutsAndLimits halibutTimeoutsAndLimits)
        {
            clientBuilder.WithHalibutTimeoutsAndLimits(halibutTimeoutsAndLimits);
            serviceBuilder.WithHalibutTimeoutsAndLimits(halibutTimeoutsAndLimits);
            return this;
        }

        public LatestClientAndLatestServiceBuilder WithServiceFactory(IServiceFactory serviceFactory)
        {
            serviceBuilder.WithServiceFactory(serviceFactory);
            return this;
        }

        public LatestClientAndLatestServiceBuilder WithAsyncService<TContract, TClientContract>(Func<TClientContract> implementation)
        {
            serviceBuilder.WithAsyncService<TContract, TClientContract>(implementation);
            return this;
        }

        IClientAndServiceBuilder IClientAndServiceBuilder.WithPortForwarding(out Reference<PortForwarder> portForwarder, Func<int, PortForwarder> portForwarderFactory)
        {
            return this.WithPortForwarding(out portForwarder, portForwarderFactory);
        }

        public LatestClientAndLatestServiceBuilder WithPortForwarding(out Reference<PortForwarder> portForwarder, Func<int, PortForwarder> portForwarderFactory)
        {
            // Based on ServiceConnectionType, the port forwarder will be created in either clientBuilder or serviceBuilder.
            clientBuilder.WithPortForwarding(out clientPortForwarderReference, portForwarderFactory);
            serviceBuilder.WithPortForwarding(out servicePortForwarderReference, portForwarderFactory);

            portForwarderReference = new Reference<PortForwarder>();
            portForwarder = portForwarderReference;

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

        IClientAndServiceBuilder IClientAndServiceBuilder.WithProxy(out Reference<HttpProxyService> proxyService)
        {
            return WithProxy(out proxyService);
        }

        public LatestClientAndLatestServiceBuilder WithProxy(out Reference<HttpProxyService> proxyService)
        {
            this.proxyFactory = new ProxyFactory();

            proxyServiceReference = new Reference<HttpProxyService>();
            proxyService = proxyServiceReference;

            return this;
        }

        public LatestClientAndLatestServiceBuilder WithPendingRequestQueueFactory(Func<ILogFactory, IPendingRequestQueueFactory> pendingRequestQueueFactory)
        {
            clientBuilder.WithPendingRequestQueueFactory(pendingRequestQueueFactory);
            return this;
        }

        public LatestClientAndLatestServiceBuilder WithPendingRequestQueueFactoryBuilder(Action<PendingRequestQueueFactoryBuilder> pendingRequestQueueFactoryBuilder)
        {
            clientBuilder.WithPendingRequestQueueFactoryBuilder(pendingRequestQueueFactoryBuilder);
            return this;
        }

        public LatestClientAndLatestServiceBuilder WithPollingReconnectRetryPolicy(Func<RetryPolicy> pollingReconnectRetryPolicy)
        {
            serviceBuilder.WithPollingReconnectRetryPolicy(pollingReconnectRetryPolicy);
            return this;
        }

        IClientAndServiceBuilder IClientAndServiceBuilder.WithHalibutLoggingLevel(LogLevel halibutLogLevel)
        {
            return WithHalibutLoggingLevel(halibutLogLevel);
        }

        public LatestClientAndLatestServiceBuilder WithHalibutLoggingLevel(LogLevel halibutLogLevel)
        {
            clientBuilder.WithHalibutLoggingLevel(halibutLogLevel);
            serviceBuilder.WithHalibutLoggingLevel(halibutLogLevel);
            return this;
        }

        public LatestClientAndLatestServiceBuilder RecordingClientLogs(out ConcurrentDictionary<string, ILog> inMemoryLoggers)
        {
            clientBuilder.RecordingClientLogs(out inMemoryLoggers);
            return this;
        }

        public LatestClientAndLatestServiceBuilder RecordingServiceLogs(out ConcurrentDictionary<string, ILog> inMemoryLoggers)
        {
            serviceBuilder.RecordingServiceLogs(out inMemoryLoggers);
            return this;
        }

        public LatestClientAndLatestServiceBuilder WithClientOnUnauthorizedClientConnect(Func<string, string, UnauthorizedClientConnectResponse> onUnauthorizedClientConnect)
        {
            clientBuilder.WithClientOnUnauthorizedClientConnect(onUnauthorizedClientConnect);
            return this;
        }

        public LatestClientAndLatestServiceBuilder WithClientTrustProvider(ITrustProvider trustProvider)
        {
            clientBuilder.WithClientTrustProvider(trustProvider);
            return this;
        }

        public LatestClientAndLatestServiceBuilder WithClientTrustingTheWrongCertificate()
        {
            clientBuilder.WithClientTrustingTheWrongCertificate();
            return this;
        }

        public LatestClientAndLatestServiceBuilder WithServiceTrustingTheWrongCertificate()
        {
            serviceBuilder.WithServiceTrustingTheWrongCertificate();
            return this;
        }

        public LatestClientAndLatestServiceBuilder WithClientRpcObserver(IRpcObserver? clientRpcObserver)
        {
            clientBuilder.WithClientRpcObserver(clientRpcObserver);
            return this;
        }

        async Task<IClientAndService> IClientAndServiceBuilder.Build(CancellationToken cancellationToken)
        {
            return await Build(cancellationToken);
        }

        public async Task<ClientAndService> Build(CancellationToken cancellationToken)
        {
            var httpProxy = proxyFactory?.WithDelaySendingSectionsOfHttpHeaders(true).Build();

            if (httpProxy != null)
            {
                await httpProxy.StartAsync();
                var httpProxyDetails = new ProxyDetails("localhost", httpProxy.Endpoint!.Port, ProxyType.HTTP);

                clientBuilder.WithProxyDetails(httpProxyDetails);
                serviceBuilder.WithProxyDetails(httpProxyDetails);

                if (proxyServiceReference is not null)
                {
                    proxyServiceReference.Value = httpProxy;
                }
            }

            var client = await clientBuilder.Build(cancellationToken);
            if (client.ListeningUri is not null)
            {
                serviceBuilder.WithListeningClient(client.ListeningUri);
            }
            
            var service = await serviceBuilder.Build(cancellationToken);

            if (portForwarderReference != null)
            {
                // Only one of these will have created a port forwarder. Use that one.
                var portForwarder = clientPortForwarderReference?.Value ?? servicePortForwarderReference?.Value;
                if (portForwarder != null)
                {
                    portForwarderReference.Value = portForwarder;
                }
            }
            return new ClientAndService(client, service, httpProxy);
        }

        public class ClientAndService : IClientAndService
        {
            readonly LatestClient client;
            readonly LatestService service;
            readonly HttpProxyService? httpProxy;

            public ClientAndService(
                LatestClient client,
                LatestService service,
                HttpProxyService? proxy)
            {
                this.client = client;
                this.service = service;

                httpProxy = proxy;
            }

            public Uri ServiceUri => service.ServiceUri;
            public HalibutRuntime Client => client.Client;
            public HalibutRuntime Service => service.Service;

            public ServiceEndPoint GetServiceEndPoint()
            {
                return client.GetServiceEndPoint(ServiceUri);
            }

            public TAsyncClientService CreateAsyncClient<TService, TAsyncClientService>(Action<ServiceEndPoint> modifyServiceEndpoint)
            {
                return client.CreateClient<TService, TAsyncClientService>(ServiceUri, modifyServiceEndpoint);
            }

            public TAsyncClientService CreateAsyncClient<TService, TAsyncClientService>(Func<ServiceEndPoint, ServiceEndPoint> modifyServiceEndpoint)
            {
                return client.CreateClient<TService, TAsyncClientService>(ServiceUri, modifyServiceEndpoint);
            }

            public TAsyncClientService CreateAsyncClient<TService, TAsyncClientService>()
            {
                return client.CreateClient<TService, TAsyncClientService>(ServiceUri);
            }

            public async ValueTask DisposeAsync()
            {
                var logger = new SerilogLoggerBuilder().Build().ForContext<ClientAndService>();

                await client.DisposeAsync();
                await service.DisposeAsync();

                void LogError(Exception e) => logger.Warning(e, "Ignoring error in dispose");
                Try.CatchingError(() => httpProxy?.Dispose(), LogError);
            }
        }
    }
}
