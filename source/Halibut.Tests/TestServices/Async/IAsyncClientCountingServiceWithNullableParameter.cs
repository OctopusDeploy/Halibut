using System.Threading.Tasks;

namespace Halibut.Tests.TestServices.Async
{
    public interface IAsyncClientCountingServiceWithNullableParameter
    {
        public Task<int> IncrementAsync();
        public Task<int> IncrementAsync(int? number);
        public Task<int> GetCurrentValueAsync();
    }
}
