using Halibut;

namespace Halibut.TestUtils.SampleProgram.v5_0_429
{
    public interface IEchoService
    {
        int LongRunningOperation();

        string SayHello(string name);

        bool Crash();

        int CountBytes(DataStream stream);
    }
}