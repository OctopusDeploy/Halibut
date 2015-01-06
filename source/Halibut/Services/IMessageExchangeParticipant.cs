using System;
using Halibut.Protocol;

namespace Halibut.Services
{
    public interface IMessageExchangeParticipant
    {
        IPendingRequestQueue SelectQueue(IdentificationMessage clientIdentification);
    }
}