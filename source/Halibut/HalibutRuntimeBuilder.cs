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

        public HalibutRuntime Build()
        {
            var serviceFactory = this.serviceFactory ?? new NullServiceFactory();
            if (serverCertificate == null) throw new ArgumentException($"Set a server certificate with {nameof(WithServerCertificate)} before calling {nameof(Build)}", nameof(serverCertificate));
            var logFactory = this.logFactory ?? new LogFactory();
            var queueFactory = this.queueFactory ?? new DefaultPendingRequestQueueFactory(logFactory);
            var trustProvider = this.trustProvider ?? new DefaultTrustProvider();
            if (messageSerializer == null)
            {
                var serializer = new MessageSerializer();
                serializer.AddToMessageContract(serviceFactory.RegisteredServiceTypes.ToArray());
                messageSerializer = serializer;
            }

            return new HalibutRuntime(serviceFactory, serverCertificate, trustProvider, queueFactory, logFactory, messageSerializer);
        }
    }
}