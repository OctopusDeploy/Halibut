using System;

namespace Halibut.SampleLoadTest
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