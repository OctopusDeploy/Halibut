using System;
using Halibut.Protocol;

namespace Halibut.Tests.TestServices
{
    public interface IEchoService
    {
        int LongRunningOperation();

        string SayHello(string name);

        bool Crash();

        int CountBytes(DataStream stream);
    }
}