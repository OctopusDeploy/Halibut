using System;
using Halibut.Diagnostics;

namespace Halibut.ServiceModel
{
    class DefaultPendingRequestQueueFactory : IPendingRequestQueueFactory
    {
        readonly ILogFactory logFactory;

        public DefaultPendingRequestQueueFactory(ILogFactory logFactory)
        {
            this.logFactory = logFactory;
        }

        public IPendingRequestQueue CreateQueue(Uri endpoint)
        {
            return new PendingRequestQueue(logFactory.ForEndpoint(endpoint));
        }
    }
}