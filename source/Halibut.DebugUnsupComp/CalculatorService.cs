using System;
using System.Linq;
using System.Threading;

namespace Halibut.DebugUnsupComp
{
    public class CalculatorService : ICalculatorService
    {
        readonly Random random = new Random();
        
        public long Add(long a, long b)
        {
            return a + b;
        }

        public long Subtract(long a, long b)
        {
            return a - b;
        }

        public int SlowWithJitter(long requestNum)
        {
            Console.Write("," + requestNum);
            Thread.Sleep(TimeSpan.FromMilliseconds(random.Next(0, 500)));
            return 1;
        }

        public int ReallySlow()
        {
            Thread.Sleep(TimeSpan.FromMilliseconds(random.Next(10_000, 15_000)));
            return 1;
        }

        public int ThisShouldThrow()
        {
            throw new NotImplementedException();
        }

        public int InfiniteRecursion()
        {
            return InfiniteRecursion();
        }

        public string SendAndReceiveString(string input)
        {
            return input;
        }
    }
}

namespace Halibut.DebugUnsupComp.DiffNamespaceSoWeCanHaveDuplicateNames
{
    public class CalculatorService2 : ICalculatorService
    {
        public string[] Add(long a, long b)
        {
            return new[]
            {
                (a + b).ToString()
            };
        }

        public long Subtract(long a, long b)
        {
            return a - b;
        }
    }
}
