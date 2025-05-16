using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Halibut.Diagnostics;
using Halibut.ServiceModel;
using Halibut.Transport.Observability;
using Halibut.Transport.Protocol;
using Halibut.Transport.Streams;
using Halibut.Util;

namespace Halibut
{
    public class HalibutRuntimeBuilder
    {
        ILogFactory? logFactory;
        IPendingRequestQueueFactory? queueFactory;
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
        IControlMessageObserver? controlMessageObserver;
        ISubscribersObserver? identityObserver;

        public HalibutRuntimeBuilder WithConnectionsObserver(IConnectionsObserver connectionsObserver)
        {
            this.connectionsObserver = connectionsObserver;
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
            this.queueFactory = queueFactory;
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

        internal HalibutRuntimeBuilder WithPollingReconnectRetryPolicy(Func<RetryPolicy> pollingReconnectRetryPolicy)
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

        public HalibutRuntimeBuilder WithSubscribersObserver(ISubscribersObserver subscribersObserver)
        {
            this.identityObserver = subscribersObserver;
            return this;
        }

        public HalibutRuntime Build()
        {
            var halibutTimeoutsAndLimits = this.halibutTimeoutsAndLimits;
            halibutTimeoutsAndLimits ??= new HalibutTimeoutsAndLimits();

            var serviceFactory = this.serviceFactory ?? new NullServiceFactory();
            if (serverCertificate == null) throw new ArgumentException($"Set a server certificate with {nameof(WithServerCertificate)} before calling {nameof(Build)}", nameof(serverCertificate));
            var logFactory = this.logFactory ?? new LogFactory();
            var queueFactory = this.queueFactory ?? new PendingRequestQueueFactoryAsync(halibutTimeoutsAndLimits, logFactory);
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
            var streamFactory = this.streamFactory ?? new StreamFactory();
            var connectionsObserver = this.connectionsObserver ?? NoOpConnectionsObserver.Instance;
            var rpcObserver = this.rpcObserver ?? new NoRpcObserver();
            var controlMessageObserver = this.controlMessageObserver ?? new NoOpControlMessageObserver();
            var identityObserver = this.identityObserver ?? NoSubscribersObserver.Instance;

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
                identityObserver);

            if (onUnauthorizedClientConnect is not null)
            {
                halibutRuntime.OnUnauthorizedClientConnect = onUnauthorizedClientConnect;
            }

            return halibutRuntime;
        }
    }
}