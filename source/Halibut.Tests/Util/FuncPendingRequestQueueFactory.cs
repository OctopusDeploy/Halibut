using System;
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
    }
}