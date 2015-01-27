using System;
using Halibut.Transport.Protocol;

namespace Halibut.ServiceModel
{
    public interface IServiceInvoker
    {
        ResponseMessage Invoke(RequestMessage requestMessage);
    }
}