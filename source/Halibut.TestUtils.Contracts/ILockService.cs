namespace Halibut.TestUtils.Contracts
{
    public interface ILockService
    {
        public void WaitForFileToBeDeleted(string fileToWaitFor, string fileSignalWhenRequestIsStarted);
    }
}