using System;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.ServiceModel
{
    public interface IPendingRequestQueueFactory
    {
        IPendingRequestQueue CreateQueue(Uri endpoint);
        Task<IPendingRequestQueue> CreateQueueAsync(Uri endpoint, CancellationToken cancellationToken);
    }
}