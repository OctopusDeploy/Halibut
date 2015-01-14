using System;
using System.Collections.Generic;
using Halibut.Protocol;

namespace Halibut.Services
{
    public interface IPendingRequestQueue
    {
        bool IsEmpty { get; }
        void ApplyResponse(ResponseMessage response);
        RequestMessage Dequeue();
    }
}