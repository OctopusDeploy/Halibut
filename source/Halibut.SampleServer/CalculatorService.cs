using System;
using Halibut.SampleContracts;

namespace Halibut.SampleServer
{
    public class CalculatorService : ICalculatorService
    {
        public long Add(long a, long b)
        {
            return a + b;
        }

        public long Subtract(long a, long b)
        {
            return a - b;
        }
    }
}