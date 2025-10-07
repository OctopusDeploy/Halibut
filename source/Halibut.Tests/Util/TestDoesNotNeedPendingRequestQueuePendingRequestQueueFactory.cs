using System;
using Halibut.ServiceModel;

namespace Halibut.Tests.Util
{
    public class TestDoesNotNeedPendingRequestQueuePendingRequestQueueFactory : IPendingRequestQueueFactory
    {
        public IPendingRequestQueue CreateQueue(Uri endpoint)
        {
            throw new Exception("This test was not configured with a queue.");
        }
    }
}