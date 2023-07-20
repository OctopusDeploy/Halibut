using System;

namespace Halibut.TestUtils.Contracts
{
    public interface IEchoService
    {
        int LongRunningOperation();

        string SayHello(string name);

        bool Crash();

        int CountBytes(DataStream stream);
    }
}
