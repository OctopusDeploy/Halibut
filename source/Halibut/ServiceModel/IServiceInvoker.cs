using System;
using Halibut.Transport.Protocol;

namespace Halibut.ServiceModel
{
    public interface IServiceInvoker
    {
        MessageEnvelope Invoke(MessageEnvelope requestMessage);
    }
}