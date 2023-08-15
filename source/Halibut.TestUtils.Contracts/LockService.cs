using System.IO;
using System.Threading;

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
}