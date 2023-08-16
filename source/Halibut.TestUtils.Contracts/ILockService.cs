using System.Threading;
using System.Threading.Tasks;

namespace Halibut.TestUtils.Contracts
{
    public interface ILockService
    {
        public void WaitForFileToBeDeleted(string fileToWaitFor, string fileSignalWhenRequestIsStarted);
    }
    
    public interface IAsyncLockService
    {
        public Task WaitForFileToBeDeletedAsync(string fileToWaitFor, string fileSignalWhenRequestIsStarted, CancellationToken cancellationToken);
    }
}
