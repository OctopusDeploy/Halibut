using System;
using System.Threading.Tasks;
using Halibut.Diagnostics;

namespace Halibut.ServiceModel
{
    class PendingRequestQueueFactoryAsync : IPendingRequestQueueFactory
    {
        readonly ILogFactory logFactory;
        readonly HalibutTimeoutsAndLimits halibutTimeoutsAndLimits;

        public PendingRequestQueueFactoryAsync(HalibutTimeoutsAndLimits halibutTimeoutsAndLimits, ILogFactory logFactory)
        {
            this.logFactory = logFactory;
            this.halibutTimeoutsAndLimits = halibutTimeoutsAndLimits;
        }

        public IPendingRequestQueue CreateQueue(Uri endpoint)
        {
            return new PendingRequestQueueAsync(halibutTimeoutsAndLimits, logFactory.ForEndpoint(endpoint));
        }

        public Task<IPendingRequestQueue> CreateQueueAsync(Uri endpoint)
        {
            return Task.FromResult(CreateQueue(endpoint));
        }
    }
}