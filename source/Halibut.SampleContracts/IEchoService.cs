using System.Threading.Tasks;

namespace Halibut.SampleContracts
{
    public interface IEchoService
    {
        Task<int> LongRunningOperation();

        Task<string> SayHello(string name);

        Task<bool> Crash();

        Task<int> CountBytes(DataStream stream);
    }
}