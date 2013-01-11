using System;
using Halibut.SampleContracts;

namespace Halibut.SampleServer
{
    public class CalculatorService : ICalculatorService
    {
        #region ICalculatorService Members

        public long Add(long a, long b)
        {
            return a + b;
        }

        public long Subtract(long a, long b)
        {
            return a - b;
        }

        #endregion
    }
}