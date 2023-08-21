using System;
using System.Threading.Tasks;
using Halibut.Transport.Protocol;

namespace Halibut.ServiceModel
{
    public interface IServiceInvoker
    {
        [Obsolete]
        ResponseMessage Invoke(RequestMessage requestMessage);

        Task<ResponseMessage> InvokeAsync(RequestMessage requestMessage);
    }
}
