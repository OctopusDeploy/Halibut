using System.Threading;
using System.Threading.Tasks;

namespace Halibut.TestUtils.Contracts
{
    public interface ISayHelloService
    {
        string SayHello(string name);
    }

    public interface IAsyncSayHelloService
    {
        Task<string> SayHelloAsync(string name, CancellationToken cancellationToken);
    }
}
