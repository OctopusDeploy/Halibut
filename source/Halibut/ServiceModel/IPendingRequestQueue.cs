using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Transport.Protocol;

namespace Halibut.ServiceModel
{
    public interface IPendingRequestQueue
    {
        bool IsEmpty { get; }
        void ApplyResponse(IResponseMessage response, ServiceEndPoint destination);
        IRequestMessage Dequeue();
        Task<IRequestMessage> DequeueAsync();
        Task<IResponseMessage> QueueAndWaitAsync(IRequestMessage request, CancellationToken cancellationToken);
    }
}