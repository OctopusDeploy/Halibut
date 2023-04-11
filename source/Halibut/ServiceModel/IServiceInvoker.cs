using System;
using Halibut.Transport.Protocol;

namespace Halibut.ServiceModel
{
    public interface IServiceInvoker
    {
        IResponseMessage Invoke(IRequestMessage requestMessage);
    }
}