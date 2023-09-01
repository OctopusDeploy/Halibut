using System.Threading.Tasks;

namespace Halibut.Tests.TestServices.Async
{
    public interface IAsyncClientEchoService
    {
        Task<int> LongRunningOperationAsync();

        Task<string> SayHelloAsync(string name);

        Task<bool> CrashAsync();

        Task<int> CountBytesAsync(DataStream stream);

        Task ReturnNothingAsync();
    }
}