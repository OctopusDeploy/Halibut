using System.Threading.Tasks;

namespace Halibut.Tests.TestServices.Async
{
    public interface IAsyncClientCountingService
    {
        public Task<int> IncrementAsync();
        public Task<int> GetCurrentValueAsync();
    }
}