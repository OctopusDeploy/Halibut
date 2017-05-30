using System;
using System.Threading.Tasks;
using Halibut.SampleContracts;

namespace Halibut.SampleServer
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