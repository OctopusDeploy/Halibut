using System.Threading.Tasks;

namespace Halibut.Tests.TestServices.Async
{
    public interface IAsyncClientLockService
    {
        Task WaitForFileToBeDeletedAsync(string fileToWaitFor, string fileSignalWhenRequestIsStarted);
    }
}
