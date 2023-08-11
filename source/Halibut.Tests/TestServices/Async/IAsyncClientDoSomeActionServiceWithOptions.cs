using System.Threading.Tasks;
using Halibut.ServiceModel;

namespace Halibut.Tests.TestServices.Async
{
    public interface IAsyncClientDoSomeActionServiceWithOptions
    {
        Task ActionAsync(HalibutProxyRequestOptions options);
    }
}
