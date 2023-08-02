using System;
using Halibut.Diagnostics;

namespace Halibut.ServiceModel
{
    class PendingRequestQueueFactoryAsync : IPendingRequestQueueFactory
    {
        readonly ILogFactory logFactory;

        public PendingRequestQueueFactoryAsync(ILogFactory logFactory)
        {
            this.logFactory = logFactory;
        }

        public IPendingRequestQueue CreateQueue(Uri endpoint)
        {
            return new PendingRequestQueueAsync(logFactory.ForEndpoint(endpoint));
        }
    }
}