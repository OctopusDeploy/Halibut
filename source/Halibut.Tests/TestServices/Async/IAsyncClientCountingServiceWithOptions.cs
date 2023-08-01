using System.Threading.Tasks;
using Halibut.ServiceModel;

namespace Halibut.Tests.TestServices.Async
{
    public interface IAsyncClientCountingServiceWithOptions
    {
        public Task<int> IncrementAsync(HalibutProxyRequestOptions halibutProxyRequestOptions);
        public Task<int> GetCurrentValueAsync(HalibutProxyRequestOptions halibutProxyRequestOptions);
    }
}