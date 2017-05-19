using System;
using System.Threading.Tasks;

namespace Halibut.Tests.TestServices
{
    public interface IEchoService
    {
        int LongRunningOperation();

        Task<string> SayHello(string name);

        bool Crash();

        int CountBytes(DataStream stream);
    }
}