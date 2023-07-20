using System.IO;
using Halibut.TestUtils.Contracts;

namespace Halibut.TestUtils.SampleProgram.Base
{
    public class DelegateLockService : ILockService
    {
        ILockService lockServiceImplementation;

        public DelegateLockService(ILockService lockServiceImplementation)
        {
            this.lockServiceImplementation = lockServiceImplementation;
        }

        public void WaitForFileToBeDeleted(string fileToWaitFor, string fileSignalWhenRequestIsStarted)
        {
            lockServiceImplementation.WaitForFileToBeDeleted(fileToWaitFor, fileSignalWhenRequestIsStarted);
        }

    }
}