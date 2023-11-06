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
        public async Task WaitForFileToBeDeletedAsync(string file, string fileSignalWhenRequestIsStarted, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            File.Create(fileSignalWhenRequestIsStarted);
            while (File.Exists(file))
            {
                await Task.Delay(20, cancellationToken);
            }
        }
    }
}
