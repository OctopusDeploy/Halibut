using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Transport.Protocol;

namespace Halibut.ServiceModel
{
    public interface IPendingRequestQueue : IAsyncDisposable
    {
        bool IsEmpty { get; }
        
        /// <summary>
        /// For testing only, use only as an indicator that the call to QueueAndWaitAsync has made the request ready for collection.
        /// The number of request that are ready for collection,
        /// OR the number of requests that are still in flight but passed the point of ready for collection.
        /// </summary>
        int Count { get; }
        Task ApplyResponse(ResponseMessage response, Guid requestActivityId);
        Task<RequestMessageWithCancellationToken?> DequeueAsync(CancellationToken cancellationToken);
        Task<ResponseMessage> QueueAndWaitAsync(RequestMessage request, CancellationToken cancellationToken);
    }
}