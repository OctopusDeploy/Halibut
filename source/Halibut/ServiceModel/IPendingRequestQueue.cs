using System;
using System.Threading.Tasks;
using Halibut.Transport.Protocol;

namespace Halibut.ServiceModel
{
    public interface IPendingRequestQueue
    {
        bool IsEmpty { get; }
        void ApplyResponse(MessageEnvelope response);
        MessageEnvelope Dequeue();
        Task<MessageEnvelope> DequeueAsync();
    }
}