using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Halibut.Diagnostics;
using Halibut.ServiceModel;
using Halibut.Transport;
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
        AsyncHalibutFeature asyncHalibutFeature = AsyncHalibutFeature.Disabled;
        Func<string, string, UnauthorizedClientConnectResponse> onUnauthorizedClientConnect;
        IClientCertificateValidatorFactory clientCertificateValidatorFactory;

        public HalibutRuntimeBuilder WithServiceFactory(IServiceFactory serviceFactory)
        {
            this.serviceFactory = serviceFactory;
            return this;
        }

        public HalibutRuntimeBuilder WithClientCertificateValidatorFactory(IClientCertificateValidatorFactory clientCertificateValidator)
        {
            this.clientCertificateValidatorFactory = clientCertificateValidator;
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

        public HalibutRuntimeBuilder WithAsyncHalibutFeatureEnabled()
        {
            return WithAsyncHalibutFeature(AsyncHalibutFeature.Enabled);
        }
        
        public HalibutRuntimeBuilder WithAsyncHalibutFeature(AsyncHalibutFeature asyncHalibutFeature)
        {
            this.asyncHalibutFeature = asyncHalibutFeature;
            return this;
        }

        internal HalibutRuntimeBuilder WithPollingReconnectRetryPolicy(Func<RetryPolicy> pollingReconnectRetryPolicy)
        {
            this.pollingReconnectRetryPolicy = pollingReconnectRetryPolicy;
            return this;
        }

        public HalibutRuntimeBuilder WithOnUnauthorizedClientConnect(Func<string, string, UnauthorizedClientConnectResponse> onUnauthorizedClientConnect)
        {
            this.onUnauthorizedClientConnect = onUnauthorizedClientConnect;
            return this;
        }

        public HalibutRuntime Build()
        {
            var serviceFactory = this.serviceFactory ?? new NullServiceFactory();
            if (serverCertificate == null) throw new ArgumentException($"Set a server certificate with {nameof(WithServerCertificate)} before calling {nameof(Build)}", nameof(serverCertificate));
            var logFactory = this.logFactory ?? new LogFactory();
#pragma warning disable CS0612
            var queueFactory = this.queueFactory ?? (asyncHalibutFeature.IsEnabled() ? new PendingRequestQueueFactoryAsync(logFactory) : new DefaultPendingRequestQueueFactory(logFactory));
#pragma warning restore CS0612
            var trustProvider = this.trustProvider ?? new DefaultTrustProvider();
            var typeRegistry = this.typeRegistry ?? new TypeRegistry();
            var clientCertificateValidatorFactory = this.clientCertificateValidatorFactory ?? new ClientCertificateValidatorFactory();

            var messageContracts = serviceFactory.RegisteredServiceTypes.ToArray();
            typeRegistry.AddToMessageContract(messageContracts);

            var builder = new MessageSerializerBuilder();
            configureMessageSerializerBuilder?.Invoke(builder);
            var messageSerializer = builder.WithTypeRegistry(typeRegistry).Build();

            var halibutRuntime = new HalibutRuntime(
                serviceFactory, 
                serverCertificate,
                trustProvider, 
                queueFactory, 
                logFactory, 
                typeRegistry,
                messageSerializer, 
                pollingReconnectRetryPolicy,
                asyncHalibutFeature,
                clientCertificateValidatorFactory);

            if (onUnauthorizedClientConnect is not null)
            {
                halibutRuntime.OnUnauthorizedClientConnect = onUnauthorizedClientConnect;
            }

            return halibutRuntime;
        }
    }
}