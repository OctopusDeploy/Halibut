using System;
using System.Collections.Generic;
using Halibut.Protocol;

namespace Halibut.Services
{
    public interface IPendingRequestQueue
    {
        bool IsEmpty { get; }
        void ApplyResponses(List<ResponseMessage> responses);
        List<RequestMessage> DequeueRequests();
    }
}