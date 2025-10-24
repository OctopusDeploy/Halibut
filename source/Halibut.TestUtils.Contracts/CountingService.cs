using System.Threading;
using System.Threading.Tasks;

namespace Halibut.TestUtils.Contracts
{
    public class CountingService : ICountingService
    {
        int count;
        public int Increment()
        {
            return Increment(1);
        }

        public int Increment(int? number)
        {
            var increment = number ?? 1;
            var counter = 0;

            for (var i = 0; i < increment; i++)
            {
                counter = Interlocked.Increment(ref count);
            }

            return counter;
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

        public async Task<int> IncrementAsync(int? number, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            return service.Increment(number);
        }

        public async Task<int> GetCurrentValueAsync(CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            return service.GetCurrentValue();
        }
        
        public int CurrentValue()
        {
            return service.GetCurrentValue();
        }
    }
}
