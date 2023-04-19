using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Transport;
using Halibut.Transport.Protocol;

namespace Halibut.ServiceModel
{
    public interface IPendingRequestQueue
    {
        bool IsEmpty { get; }
        void ApplyResponse(ResponseMessageWithTransferStatistics response, ServiceEndPoint destination);
        RequestMessage Dequeue();
        Task<RequestMessage> DequeueAsync();
        Task<ResponseMessageWithTransferStatistics> QueueAndWaitAsync(RequestMessage request, CancellationToken cancellationToken);
    }
}