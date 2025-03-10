using System;
using System.Threading.Tasks;
using Halibut.ServiceModel;

namespace Halibut.Tests.Util
{
    public class FuncPendingRequestQueueFactory : IPendingRequestQueueFactory
    {
        Func<Uri, IPendingRequestQueue> createQueue;

        public FuncPendingRequestQueueFactory(Func<Uri, IPendingRequestQueue> createQueue)
        {
            this.createQueue = createQueue;
        }

        public IPendingRequestQueue CreateQueue(Uri endpoint)
        {
            return createQueue(endpoint);
        }

        public Task<IPendingRequestQueue> CreateQueueAsync(Uri endpoint)
        {
            return Task.FromResult(createQueue(endpoint));
        }
    }
}