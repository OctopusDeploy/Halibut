using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Transport.Protocol;

namespace Halibut.ServiceModel
{
    public interface IPendingRequestQueue : IAsyncDisposable
    {
        bool IsEmpty { get; }
        int Count { get; }
        Task ApplyResponse(ResponseMessage response, Guid requestActivityId);
        Task<RequestMessageWithCancellationToken?> DequeueAsync(CancellationToken cancellationToken);
        Task<ResponseMessage> QueueAndWaitAsync(RequestMessage request, CancellationToken cancellationToken);
    }
}