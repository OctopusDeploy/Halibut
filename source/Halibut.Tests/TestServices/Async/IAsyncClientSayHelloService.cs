using System.Threading.Tasks;

namespace Halibut.Tests.TestServices.Async
{
    public interface IAsyncClientSayHelloService
    {
        Task<string> SayHelloAsync(string name);
    }
}
