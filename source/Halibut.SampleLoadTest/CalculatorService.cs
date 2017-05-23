using System;
using System.Threading.Tasks;

namespace Halibut.SampleLoadTest
{
    public class CalculatorService : ICalculatorService
    {
        public Task<long> Add(long a, long b)
        {
            return Task.FromResult(a + b);
        }

        public Task<long> Subtract(long a, long b)
        {
            return Task.FromResult(a - b);
        }
    }
}