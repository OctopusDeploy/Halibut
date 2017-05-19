using System.Threading.Tasks;

namespace Halibut.Tests.TestServices
{
    public interface IEchoService
    {
        Task<int> LongRunningOperation();

        Task<string> SayHello(string name);

        Task<bool> Crash();

        Task<int> CountBytes(DataStream stream);
    }
}