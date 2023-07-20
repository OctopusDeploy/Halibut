using System.Threading;

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
}