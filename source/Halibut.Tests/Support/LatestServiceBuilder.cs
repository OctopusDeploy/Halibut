using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.Diagnostics.LogCreators;
using Halibut.ServiceModel;
using Halibut.Tests.Builders;
using Halibut.Tests.Support.Logging;
using Halibut.Transport.Observability;
using Halibut.Transport.Streams;
using Halibut.Util;
using Octopus.TestPortForwarder;
using ILog = Halibut.Diagnostics.ILog;

namespace Halibut.Tests.Support
{
    public class LatestServiceBuilder : IServiceBuilder
    {
        public static Uri PollingTentacleServiceUri => new("poll://SQ-TENTAPOLL");
        public static Uri PollingOverWebSocketTentacleServiceUri => new("poll://SQ-TENTAPOLL");
        public static Uri ListeningTentacleServiceUri(int port) => new($"https://localhost:{port}");

        readonly ServiceConnectionType serviceConnectionType;
        readonly ServiceFactoryBuilder serviceFactoryBuilder = new();

        CertAndThumbprint serviceCertAndThumbprint;

        IServiceFactory? serviceFactory;
        string serviceTrustsThumbprint;

        readonly List<Uri> listeningClientUris = new();
        Func<int, PortForwarder>? portForwarderFactory;
        Reference<PortForwarder>? portForwarderReference;
        Func<RetryPolicy>? pollingReconnectRetryPolicy;
        ProxyDetails? proxyDetails;
        LogLevel halibutLogLevel = LogLevel.Trace;
        ConcurrentDictionary<string, ILog>? serviceInMemoryLoggers;
        HalibutTimeoutsAndLimits halibutTimeoutsAndLimits = new HalibutTimeoutsAndLimitsForTestsBuilder().Build();
        IStreamFactory? serviceStreamFactory;
        IConnectionsObserver? serviceConnectionsObserver;
        int pollingConnectionCount = 1;
        IControlMessageObserver? controlMessageObserver;

        public LatestServiceBuilder(
            ServiceConnectionType serviceConnectionType,
            CertAndThumbprint clientCertAndThumbprint,
            CertAndThumbprint serviceCertAndThumbprint)
        {
            this.serviceConnectionType = serviceConnectionType;
            this.serviceCertAndThumbprint = serviceCertAndThumbprint;
            serviceTrustsThumbprint = clientCertAndThumbprint.Thumbprint;
        }

        public static LatestServiceBuilder ForServiceConnectionType(ServiceConnectionType serviceConnectionType)
        {
            switch (serviceConnectionType)
            {
                case ServiceConnectionType.Polling:
                    return new LatestServiceBuilder(ServiceConnectionType.Polling, CertAndThumbprint.Octopus, CertAndThumbprint.TentaclePolling);
                case ServiceConnectionType.Listening:
                    return new LatestServiceBuilder(ServiceConnectionType.Listening, CertAndThumbprint.Octopus, CertAndThumbprint.TentacleListening);
                case ServiceConnectionType.PollingOverWebSocket:
                    return new LatestServiceBuilder(ServiceConnectionType.PollingOverWebSocket, CertAndThumbprint.Ssl, CertAndThumbprint.TentaclePolling);
                default:
                    throw new ArgumentOutOfRangeException(nameof(serviceConnectionType), serviceConnectionType, null);
            }
        }

        public LatestServiceBuilder WithListeningClient(Uri listeningClient)
        {
            listeningClientUris.Add(listeningClient);

            return this;
        }

        public LatestServiceBuilder WithListeningClients(IEnumerable<Uri> listeningClientUris)
        {
            this.listeningClientUris.AddRange(listeningClientUris);

            return this;
        }

        public LatestServiceBuilder WithCertificate(CertAndThumbprint serviceCertAndThumbprint)
        {
            this.serviceCertAndThumbprint = serviceCertAndThumbprint;
            return this;
        }

        public LatestServiceBuilder WithTrustedThumbprint(string trustedThumbprint)
        {
            this.serviceTrustsThumbprint = trustedThumbprint;
            return this;
        }
        
        public LatestServiceBuilder WithStreamFactory(IStreamFactory streamFactory)
        {
            this.serviceStreamFactory = streamFactory;
            return this;
        }

        public LatestServiceBuilder WithServiceConnectionsObserver(IConnectionsObserver connectionsObserver)
        {
            this.serviceConnectionsObserver = connectionsObserver;
            return this;
        }

        public LatestServiceBuilder WithHalibutTimeoutsAndLimits(HalibutTimeoutsAndLimits halibutTimeoutsAndLimits)
        {
            this.halibutTimeoutsAndLimits = halibutTimeoutsAndLimits;
            return this;
        }

        public LatestServiceBuilder WithServiceFactory(IServiceFactory serviceFactory)
        {
            this.serviceFactory = serviceFactory;
            return this;
        }

        public LatestServiceBuilder WithAsyncService<TContract, TClientContract>(Func<TClientContract> implementation)
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

        IServiceBuilder IServiceBuilder.WithPortForwarding(out Reference<PortForwarder> portForwarder, Func<int, PortForwarder> portForwarderFactory)
        {
            return WithPortForwarding(out portForwarder, portForwarderFactory);
        }

        public LatestServiceBuilder WithPortForwarding(out Reference<PortForwarder> portForwarder, Func<int, PortForwarder> portForwarderFactory)
        {
            if (this.portForwarderFactory != null)
            {
                throw new NotSupportedException("A PortForwarderFactory is already registered with the Builder. Only one PortForwarder is supported.");
            }

            this.portForwarderFactory = portForwarderFactory;

            portForwarderReference = new Reference<PortForwarder>();
            portForwarder = portForwarderReference;

            return this;
        }

        public LatestServiceBuilder WithProxyDetails(ProxyDetails? proxyDetails)
        {
            this.proxyDetails = proxyDetails;
            return this;
        }

        public LatestServiceBuilder WithPollingReconnectRetryPolicy(Func<RetryPolicy> pollingReconnectRetryPolicy)
        {
            this.pollingReconnectRetryPolicy = pollingReconnectRetryPolicy;
            return this;
        }

        public LatestServiceBuilder WithHalibutLoggingLevel(LogLevel halibutLogLevel)
        {
            this.halibutLogLevel = halibutLogLevel;
            return this;
        }

        public LatestServiceBuilder RecordingServiceLogs(out ConcurrentDictionary<string, ILog> inMemoryLoggers)
        {
            inMemoryLoggers = new ConcurrentDictionary<string, ILog>();
            this.serviceInMemoryLoggers = inMemoryLoggers;
            return this;
        }

        public LatestServiceBuilder WithServiceTrustingTheWrongCertificate()
        {
            serviceTrustsThumbprint = CertAndThumbprint.Wrong.Thumbprint;
            return this;
        }

        async Task<IService> IServiceBuilder.Build(CancellationToken cancellationToken)
        {
            return await Build(cancellationToken);
        }

        public async Task<LatestService> Build(CancellationToken cancellationToken)
        {
            //TODO: @server-at-scale - We don't need to be async. But this is left here to see if we need to add it back some day. We can decide later if we wish to make this sync.
            await Task.CompletedTask;

            serviceFactory ??= serviceFactoryBuilder.Build();

            var serviceBuilder = new HalibutRuntimeBuilder()
                .WithServiceFactory(serviceFactory)
                .WithServerCertificate(serviceCertAndThumbprint.Certificate2)
                .WithHalibutTimeoutsAndLimits(halibutTimeoutsAndLimits)
                .WithStreamFactoryIfNotNull(serviceStreamFactory)
                .WithControlMessageObserverIfNotNull(controlMessageObserver)
                .WithConnectionsObserver(serviceConnectionsObserver!)
                .WithLogFactory(BuildServiceLogger());

            if (pollingReconnectRetryPolicy != null) serviceBuilder.WithPollingReconnectRetryPolicy(pollingReconnectRetryPolicy);
            var service = serviceBuilder.Build();

            PortForwarder? portForwarder = null;
            Uri serviceUri;

            if (serviceConnectionType == ServiceConnectionType.Polling)
            {
                serviceUri = PollingTentacleServiceUri;

                foreach (var listeningClientUri in listeningClientUris)
                {
                    for (var i = 0; i < pollingConnectionCount; i++)
                    {
                        service.Poll(
                            serviceUri,
                            new ServiceEndPoint(listeningClientUri, serviceTrustsThumbprint, proxyDetails, service.TimeoutsAndLimits),
                            cancellationToken);
                    }
                }
            }
            else if (serviceConnectionType == ServiceConnectionType.PollingOverWebSocket)
            {
                serviceUri = PollingOverWebSocketTentacleServiceUri;

                foreach (var listeningClientUri in listeningClientUris)
                {
                    service.Poll(
                        serviceUri,
                        new ServiceEndPoint(listeningClientUri, serviceTrustsThumbprint, proxyDetails, service.TimeoutsAndLimits),
                        cancellationToken);
                }
            }
            else if (serviceConnectionType == ServiceConnectionType.Listening)
            {
                service.Trust(serviceTrustsThumbprint);
                var listenPort = service.Listen();

                portForwarder = portForwarderFactory?.Invoke(listenPort);
                if (portForwarder != null)
                {
                    listenPort = portForwarder.ListeningPort;
                }

                serviceUri = ListeningTentacleServiceUri(listenPort);
            }
            else
            {
                throw new NotSupportedException();
            }

            if (portForwarderReference != null && portForwarder != null)
            {
                portForwarderReference.Value = portForwarder;
            }

            return new LatestService(service, serviceUri, portForwarder);
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
                        return new[] { logger };
                    }
                )
                .ToCachingLogFactory();
        }

        public LatestServiceBuilder WithMultiplePollingConnections(int count)
        {
            if (serviceConnectionType != ServiceConnectionType.Polling)
                return this;

            pollingConnectionCount = count;
            return this;
        }

        public LatestServiceBuilder WithControlMessageObserver(IControlMessageObserver controlMessageObserver)
        {
            this.controlMessageObserver = controlMessageObserver;
            return this;
        }
    }
}