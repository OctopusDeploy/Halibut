using Halibut.ServiceModel;

namespace Halibut.Tests.TestServices.SyncClientWithOptions
{
    public interface ISyncClientCountingServiceWithOptions
    {
        public int Increment(HalibutProxyRequestOptions halibutProxyRequestOptions);
        public int GetCurrentValue(HalibutProxyRequestOptions halibutProxyRequestOptions);
    }
}