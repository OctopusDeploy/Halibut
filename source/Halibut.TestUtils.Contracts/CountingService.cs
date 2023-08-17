using System.Threading;
using System.Threading.Tasks;

namespace Halibut.TestUtils.Contracts
{
    public class CountingService : ICountingService
    {
        int count;
        public int Increment()
        {
            return Interlocked.Increment(ref count);
        }

        public int GetCurrentValue()
        {
            return count;
        }
    }
    
    public class AsyncCountingService : IAsyncCountingService
    {
        ICountingService service = new CountingService();
        public async Task<int> IncrementAsync(CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            return service.Increment();
        }

        public async Task<int> GetCurrentValueAsync(CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            return service.GetCurrentValue();
        }
    }
}
