using System.Threading;
using System.Threading.Tasks;

namespace Halibut.TestUtils.Contracts
{
    public interface IBrokenConventionService
    {
        int GetRandomNumberMissingSuffix();
        int GetRandomNumberMissingCancellationToken();
        string SayHelloMissingSuffix(string name);
        string SayHelloMissingCancellationToken(string name);
    }

    public interface IAsyncBrokenConventionService
    {
        // Convention says these async methods should finish with 'Async'
        // and take a CancellationToken
        Task<int> GetRandomNumberMissingSuffix(CancellationToken cancellationToken);
        Task<int> GetRandomNumberMissingCancellationTokenAsync();
        
        Task<string> SayHelloMissingSuffix(string name, CancellationToken cancellationToken);
        Task<string> SayHelloMissingCancellationTokenAsync(string name);
    }
}
