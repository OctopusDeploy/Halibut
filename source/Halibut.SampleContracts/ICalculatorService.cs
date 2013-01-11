using System;

namespace Halibut.SampleContracts
{
    public interface ICalculatorService
    {
        long Add(long a, long b);
        long Subtract(long a, long b);
    }
}
