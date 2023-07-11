using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Halibut.Diagnostics;
using Halibut.ServiceModel;
using Halibut.Transport.Protocol;
using Halibut.Util;

namespace Halibut
{
    public class HalibutRuntimeBuilder
    {
        ILogFactory logFactory;
        IPendingRequestQueueFactory queueFactory;
        X509Certificate2 serverCertificate;
        IServiceFactory serviceFactory;
        ITrustProvider trustProvider;
        Action<MessageSerializerBuilder> configureMessageSerializerBuilder;
        ITypeRegistry typeRegistry;
        Func<RetryPolicy> pollingReconnectRetryPolicy = RetryPolicy.Create;

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
            this.typeRegistry = typeRegistry;
            return this;
        }

        internal HalibutRuntimeBuilder WithPollingReconnectRetryPolicy(Func<RetryPolicy> pollingReconnectRetryPolicy)
        {
            this.pollingReconnectRetryPolicy = pollingReconnectRetryPolicy;
            return this;
        }

        public HalibutRuntime Build()
        {
            var serviceFactory = this.serviceFactory ?? new NullServiceFactory();
            if (serverCertificate == null) throw new ArgumentException($"Set a server certificate with {nameof(WithServerCertificate)} before calling {nameof(Build)}", nameof(serverCertificate));
            var logFactory = this.logFactory ?? new LogFactory();
            var queueFactory = this.queueFactory ?? new DefaultPendingRequestQueueFactory(logFactory);
            var trustProvider = this.trustProvider ?? new DefaultTrustProvider();
            var typeRegistry = this.typeRegistry ?? new TypeRegistry();

            var messageContracts = serviceFactory.RegisteredServiceTypes.ToArray();
            typeRegistry.AddToMessageContract(messageContracts);

            var builder = new MessageSerializerBuilder();
            configureMessageSerializerBuilder?.Invoke(builder);
            var messageSerializer = builder.WithTypeRegistry(typeRegistry).Build();

            return new HalibutRuntime(serviceFactory, serverCertificate, trustProvider, queueFactory, logFactory, typeRegistry, messageSerializer, pollingReconnectRetryPolicy);
        }
    }
}