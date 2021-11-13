using System;

namespace Halibut.ServiceModel
{
    public interface IPendingRequestQueueFactory
    {
        IPendingRequestQueue CreateQueue(Uri endpoint);
    }
}