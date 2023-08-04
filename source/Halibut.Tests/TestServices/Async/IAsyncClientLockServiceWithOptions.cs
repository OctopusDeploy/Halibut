using System;
using System.Threading.Tasks;
using Halibut.ServiceModel;

namespace Halibut.Tests.TestServices.Async
{
    public interface IAsyncClientLockServiceWithOptions
    {
        public Task WaitForFileToBeDeletedAsync(string fileToWaitFor, string fileSignalWhenRequestIsStarted, HalibutProxyRequestOptions halibutProxyRequestOptions);
    }
}