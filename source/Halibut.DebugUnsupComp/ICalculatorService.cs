using System;

namespace Halibut.DebugUnsupComp
{
    public interface ICalculatorService
    {
        long Add(long a, long b);
        
        long Subtract(long a, long b);

        int SlowWithJitter(long requestNum);

        int ReallySlow();

        int ThisShouldThrow();

        int InfiniteRecursion();

        string SendAndReceiveString(string input);
    }
}

namespace Halibut.DebugUnsupComp.DiffNamespaceSoWeCanHaveDuplicateNames
{
    public interface ICalculatorService
    {
        string[] Add(long a, long b);
        long Subtract(long a, long b);
    }
}