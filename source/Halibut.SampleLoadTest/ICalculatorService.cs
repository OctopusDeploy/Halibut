using System;
using System.Threading.Tasks;

namespace Halibut.SampleLoadTest
{
    public interface ICalculatorService
    {
        Task<long> Add(long a, long b);
        Task<long> Subtract(long a, long b);
    }
}