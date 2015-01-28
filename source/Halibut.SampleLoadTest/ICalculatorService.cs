using System;

namespace Halibut.SampleLoadTest
{
    public interface ICalculatorService
    {
        long Add(long a, long b);
        long Subtract(long a, long b);
    }
}