using Halibut.TestUtils.Contracts;

namespace Halibut.TestUtils.SampleProgram.Base
{
    public class DelegateCountingService : ICountingService
    {
        ICountingService countingService;

        public DelegateCountingService(ICountingService countingService)
        {
            this.countingService = countingService;
        }

        public int Increment()
        {
            return countingService.Increment();
        }

        public int Increment(int? number)
        {
            return countingService.Increment(number);
        }

        public int GetCurrentValue()
        {
            return countingService.GetCurrentValue();
        }
    }
}