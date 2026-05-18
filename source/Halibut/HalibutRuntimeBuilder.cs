using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Halibut.Diagnostics;
using Halibut.Queue;
using Halibut.Queue.MessageStreamWrapping;
using Halibut.ServiceModel;
using Halibut.Transport;
using Halibut.Transport.Observability;
using Halibut.Transport.Protocol;
using Halibut.Transport.Streams;
using Halibut.Util;

namespace Halibut
{
    public class HalibutRuntimeBuilder
    {
        ILogFactory? logFactory;
        Func<QueueMessageSerializer, IPendingRequestQueueFactory>? queueFactoryFactory;
        X509Certificate2? serverCertificate;
        IServiceFactory? serviceFactory;
        ITrustProvider? trustProvider;
        Action<MessageSerializerBuilder>? configureMessageSerializerBuilder;
        ITypeRegistry? typeRegistry;
        Action<TypeRegistryBuilder>? configureTypeRegisterBuilder;
        Func<RetryPolicy> pollingReconnectRetryPolicy = RetryPolicy.Create;
        Func<string, string, UnauthorizedClientConnectResponse>? onUnauthorizedClientConnect;
        HalibutTimeoutsAndLimits? halibutTimeoutsAndLimits;
        IStreamFactory? streamFactory;
        IRpcObserver? rpcObserver;
        IConnectionsObserver? connectionsObserver;
        ISecureConnectionObserver? secureConnectionObserver;
        IControlMessageObserver? controlMessageObserver;
        MessageStreamWrappers queueMessageStreamWrappers = new();
        ISslConfigurationProvider? sslConfigurationProvider;
        ISubscriberObserver? subscriberObserver;

        public HalibutRuntimeBuilder WithQueueMessageStreamWrappers(MessageStreamWrappers queueMessageStreamWrappers)
        {
            this.queueMessageStreamWrappers = queueMessageStreamWrappers;
            return this;
        }
        
        public HalibutRuntimeBuilder WithConnectionsObserver(IConnectionsObserver connectionsObserver)
        {
            this.connectionsObserver = connectionsObserver;
            return this;
        }

        public HalibutRuntimeBuilder WithSecureConnectionObserver(ISecureConnectionObserver secureConnectionsObserver)
        {
            this.secureConnectionObserver = secureConnectionsObserver;
            return this;
        }

        public HalibutRuntimeBuilder WithSslConfigurationProvider(ISslConfigurationProvider sslConfigurationProvider)
        {
            this.sslConfigurationProvider = sslConfigurationProvider;
            return this;
        }

        public HalibutRuntimeBuilder WithSubscriptionObserver(ISubscriberObserver subscriberObserver)
        {
            this.subscriberObserver = subscriberObserver;
            return this;
        }

        internal HalibutRuntimeBuilder WithStreamFactory(IStreamFactory streamFactory)
        {
            this.streamFactory = streamFactory;
            return this;
        }

        public HalibutRuntimeBuilder WithHalibutTimeoutsAndLimits(HalibutTimeoutsAndLimits halibutTimeoutsAndLimits)
        {
            this.halibutTimeoutsAndLimits = halibutTimeoutsAndLimits;
            return this;
        }

        public HalibutRuntimeBuilder WithServiceFactory(IServiceFactory serviceFactory)
        {
            this.serviceFactory = serviceFactory;
            return this;
        }

        public HalibutRuntimeBuilder WithServerCertificate(X509Certificate2 serverCertificate)
        {
            this.serverCertificate = serverCertificate;
            return this;
        }

        public HalibutRuntimeBuilder WithTrustProvider(ITrustProvider trustProvider)
        {
            this.trustProvider = trustProvider;
            return this;
        }

        public HalibutRuntimeBuilder WithPendingRequestQueueFactory(IPendingRequestQueueFactory queueFactory)
        {
            this.queueFactoryFactory = _ => queueFactory;
            return this;
        }
        
        public HalibutRuntimeBuilder WithPendingRequestQueueFactory(Func<QueueMessageSerializer, IPendingRequestQueueFactory> queueFactory)
        {
            this.queueFactoryFactory = queueFactory;
            return this;
        }

        public HalibutRuntimeBuilder WithLogFactory(ILogFactory logFactory)
        {
            this.logFactory = logFactory;
            return this;
        }

        public HalibutRuntimeBuilder WithMessageSerializer(Action<MessageSerializerBuilder> configureBuilder)
        {
            configureMessageSerializerBuilder = configureBuilder;
            return this;
        }

        public HalibutRuntimeBuilder WithTypeRegistry(ITypeRegistry typeRegistry)
        {
            if (configureTypeRegisterBuilder != null)
                throw new InvalidOperationException("A TypeRegistryBuilder configuration has already been provided.");

            this.typeRegistry = typeRegistry;

            return this;
        }

        public HalibutRuntimeBuilder WithTypeRegistry(Action<TypeRegistryBuilder> configureBuilder)
        {
            if (typeRegistry != null)
                throw new InvalidOperationException("A custom ITypeRegistry has already been provided.");

            configureTypeRegisterBuilder = configureBuilder;
            return this;
        }

        public HalibutRuntimeBuilder WithPollingReconnectRetryPolicy(Func<RetryPolicy> pollingReconnectRetryPolicy)
        {
            this.pollingReconnectRetryPolicy = pollingReconnectRetryPolicy;
            return this;
        }

        internal HalibutRuntimeBuilder WithControlMessageObserver(IControlMessageObserver controlMessageObserver)
        {
            this.controlMessageObserver = controlMessageObserver;
            return this;
        }

        public HalibutRuntimeBuilder WithOnUnauthorizedClientConnect(Func<string, string, UnauthorizedClientConnectResponse> onUnauthorizedClientConnect)
        {
            this.onUnauthorizedClientConnect = onUnauthorizedClientConnect;
            return this;
        }

        public HalibutRuntimeBuilder WithRpcObserver(IRpcObserver rpcObserver)
        {
            this.rpcObserver = rpcObserver;
            return this;
        }

        public HalibutRuntime Build()
        {
            var halibutTimeoutsAndLimits = this.halibutTimeoutsAndLimits;
            halibutTimeoutsAndLimits ??= new HalibutTimeoutsAndLimits();

            var serviceFactory = this.serviceFactory ?? new NullServiceFactory();
            if (serverCertificate == null) throw new ArgumentException($"Set a server certificate with {nameof(WithServerCertificate)} before calling {nameof(Build)}", nameof(serverCertificate));
            var logFactory = this.logFactory ?? new LogFactory();
            
            var trustProvider = this.trustProvider ?? new DefaultTrustProvider();

            //use either the supplied type registry, or configure the default one
            ITypeRegistry typeRegistry;
            if (this.typeRegistry != null)
                typeRegistry = this.typeRegistry;
            else
            {
                var typeRegistryBuilder = new TypeRegistryBuilder();
                configureTypeRegisterBuilder?.Invoke(typeRegistryBuilder);
                typeRegistry = typeRegistryBuilder.Build();
            }

            var messageContracts = serviceFactory.RegisteredServiceTypes.ToArray();
            typeRegistry.AddToMessageContract(messageContracts);

            var builder = new MessageSerializerBuilder(logFactory);
            configureMessageSerializerBuilder?.Invoke(builder);
            var messageSerializer = builder.WithTypeRegistry(typeRegistry).Build();
            
            var queueMessageSerializer = new QueueMessageSerializer(messageSerializer.CreateStreamCapturingSerializer, queueMessageStreamWrappers);
            var queueFactory = this.queueFactoryFactory?.Invoke(queueMessageSerializer)
                               ?? new PendingRequestQueueFactoryAsync(halibutTimeoutsAndLimits, logFactory);
            
            var streamFactory = this.streamFactory ?? new StreamFactory();
            var connectionsObserver = this.connectionsObserver ?? NoOpConnectionsObserver.Instance;
            var secureConnectionObserver = this.secureConnectionObserver ?? NoOpSecureConnectionObserver.Instance;
            var rpcObserver = this.rpcObserver ?? new NoRpcObserver();
            var controlMessageObserver = this.controlMessageObserver ?? new NoOpControlMessageObserver();
            var sslConfigurationProvider = this.sslConfigurationProvider ?? SslConfiguration.Default;
            var subscriberObserver = this.subscriberObserver ?? new NullSubscriberObserver();

            var halibutRuntime = new HalibutRuntime(
                serviceFactory,
                serverCertificate,
                trustProvider,
                queueFactory,
                logFactory,
                typeRegistry,
                messageSerializer,
                pollingReconnectRetryPolicy,
                halibutTimeoutsAndLimits,
                streamFactory,
                rpcObserver,
                connectionsObserver,
                controlMessageObserver,
                secureConnectionObserver,
                sslConfigurationProvider,
                subscriberObserver
            );

            if (onUnauthorizedClientConnect is not null)
            {
                halibutRuntime.OnUnauthorizedClientConnect = onUnauthorizedClientConnect;
            }

            return halibutRuntime;
        }
    }
}
