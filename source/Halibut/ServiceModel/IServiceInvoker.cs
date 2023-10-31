using System.Threading.Tasks;
using Halibut.Transport.Protocol;

namespace Halibut.ServiceModel
{
    public interface IServiceInvoker
    {
        Task<ResponseMessage> InvokeAsync(RequestMessage requestMessage);
    }
}
