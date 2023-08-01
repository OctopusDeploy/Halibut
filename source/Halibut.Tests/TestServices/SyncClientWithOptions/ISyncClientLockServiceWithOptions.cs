using Halibut.ServiceModel;

namespace Halibut.Tests.TestServices.SyncClientWithOptions
{
    public interface ISyncClientLockServiceWithOptions
    {
        public void WaitForFileToBeDeleted(string fileToWaitFor, string fileSignalWhenRequestIsStarted, HalibutProxyRequestOptions halibutProxyRequestOptions);
    }
}