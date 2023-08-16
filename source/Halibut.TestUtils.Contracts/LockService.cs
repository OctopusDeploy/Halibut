using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.TestUtils.Contracts
{
    public class LockService : ILockService
    {
        public void WaitForFileToBeDeleted(string file, string fileSignalWhenRequestIsStarted)
        {
            File.Create(fileSignalWhenRequestIsStarted);
            while (File.Exists(file))
            {
                Thread.Sleep(20);
            }
        }
    }

    public class AsyncLockService : IAsyncLockService
    {
        LockService service = new();
        
        public async Task WaitForFileToBeDeletedAsync(string fileToWaitFor, string fileSignalWhenRequestIsStarted, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            service.WaitForFileToBeDeleted(fileToWaitFor, fileSignalWhenRequestIsStarted);
        }
    }
}
