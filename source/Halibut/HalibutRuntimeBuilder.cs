using System;
using System.Security.Cryptography.X509Certificates;
using Halibut.Diagnostics;
using Halibut.ServiceModel;

namespace Halibut
{
    public class HalibutRuntimeBuilder
    {
        ILogFactory logFactory = new LogFactory();
        IPendingRequestQueueFactory queueFactory;
        X509Certificate2 serverCertificate;
        IServiceFactory serviceFactory = new NullServiceFactory();
        ITrustProvider trustProvider = new DefaultTrustProvider();

        public HalibutRuntimeBuilder()
        {
            queueFactory = new DefaultPendingRequestQueueFactory(logFactory);
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

        public HalibutRuntime Build()
        {
            return new HalibutRuntime(serviceFactory, serverCertificate, trustProvider, queueFactory, logFactory);
        }
    }
}