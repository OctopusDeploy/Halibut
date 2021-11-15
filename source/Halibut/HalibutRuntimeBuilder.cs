using System;
using System.Security.Cryptography.X509Certificates;
using Halibut.Diagnostics;
using Halibut.ServiceModel;

namespace Halibut
{
    public class HalibutRuntimeBuilder
    {
        ILogFactory logFactory;
        IPendingRequestQueueFactory queueFactory;
        X509Certificate2 serverCertificate;
        IServiceFactory serviceFactory;
        ITrustProvider trustProvider;

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
            if (serverCertificate == null) throw new ArgumentException($"Set a server certificate with {nameof(WithServerCertificate)} before calling {nameof(Build)}", nameof(serverCertificate));
            if (logFactory == null) logFactory = new LogFactory();
            if (queueFactory == null) queueFactory = new DefaultPendingRequestQueueFactory(logFactory);
            if (serviceFactory == null) serviceFactory = new NullServiceFactory();
            if (trustProvider == null) trustProvider = new DefaultTrustProvider();

            return new HalibutRuntime(serviceFactory, serverCertificate, trustProvider, queueFactory, logFactory);
        }
    }
}