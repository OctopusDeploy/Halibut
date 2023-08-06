using System.Threading.Tasks;
using Halibut.ServiceModel;

namespace Halibut.Tests.TestServices.Async
{
    public interface IAsyncClientEchoServiceWithOptions
    {
        Task<int> LongRunningOperationAsync(HalibutProxyRequestOptions halibutProxyRequestOptions);

        Task<string> SayHelloAsync(string name, HalibutProxyRequestOptions halibutProxyRequestOptions);

        Task<bool> CrashAsync(HalibutProxyRequestOptions halibutProxyRequestOptions);

        Task<int> CountBytesAsync(DataStream stream, HalibutProxyRequestOptions halibutProxyRequestOptions);
    }
}