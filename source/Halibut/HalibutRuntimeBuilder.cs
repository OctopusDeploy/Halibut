using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Halibut.Diagnostics;
using Halibut.ServiceModel;
using Halibut.Transport.Protocol;

namespace Halibut
{
    public class HalibutRuntimeBuilder
    {
        ILogFactory logFactory;
        IPendingRequestQueueFactory queueFactory;
        X509Certificate2 serverCertificate;
        IServiceFactory serviceFactory;
        ITrustProvider trustProvider;
        IMessageSerializer messageSerializer;
        IServiceInvoker serviceInvoker;
        ITypeRegistry typeRegistry;

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

        public HalibutRuntimeBuilder WithMessageSerializer(IMessageSerializer messageSerializer)
        {
            this.messageSerializer = messageSerializer;
            return this;
        }

        public HalibutRuntimeBuilder WithServiceInvoker(IServiceInvoker serviceInvoker)
        {
            this.serviceInvoker = serviceInvoker;
            return this;
        }

        public HalibutRuntimeBuilder WithTypeRegistry(ITypeRegistry typeRegistry)
        {
            this.typeRegistry = typeRegistry;
            return this;
        }

        public HalibutRuntime Build()
        {
            if (serviceFactory == null) serviceFactory = new NullServiceFactory();
            if (serverCertificate == null) throw new ArgumentException($"Set a server certificate with {nameof(WithServerCertificate)} before calling {nameof(Build)}", nameof(serverCertificate));
            if (logFactory == null) logFactory = new LogFactory();
            if (queueFactory == null) queueFactory = new DefaultPendingRequestQueueFactory(logFactory);
            if (trustProvider == null) trustProvider = new DefaultTrustProvider();
            if (serviceInvoker == null) serviceInvoker = new ServiceInvoker(serviceFactory);
            if (typeRegistry == null)
            {
                var messageContracts = serviceFactory.RegisteredServiceTypes.ToArray();
                typeRegistry = new TypeRegistry();
                typeRegistry.AddToMessageContract(messageContracts);
            }
            if (messageSerializer == null)
            {
                messageSerializer = new MessageSerializerBuilder()
                    .WithTypeRegistry(typeRegistry)
                    .Build();
            }

            return new HalibutRuntime(serverCertificate, trustProvider, queueFactory, logFactory, typeRegistry, messageSerializer, serviceInvoker);
        }
    }
}