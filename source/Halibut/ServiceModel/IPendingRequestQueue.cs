using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Transport.Protocol;

namespace Halibut.ServiceModel
{
    public interface IPendingRequestQueue
    {
        bool IsEmpty { get; }
        void ApplyResponse(ResponseMessage response);
        RequestMessage Dequeue();
        Task<RequestMessage> DequeueAsync();
        ResponseMessage QueueAndWait(RequestMessage request, CancellationToken cancellationToken);
    }
}