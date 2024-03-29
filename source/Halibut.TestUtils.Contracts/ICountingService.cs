using System.Threading;
using System.Threading.Tasks;

namespace Halibut.TestUtils.Contracts
{
    public interface ICountingService
    {
        public int Increment();
        public int GetCurrentValue();
    }

    public interface IAsyncCountingService
    {
        public Task<int> IncrementAsync(CancellationToken cancellationToken);
        public Task<int> GetCurrentValueAsync(CancellationToken cancellationToken);
    }
}
