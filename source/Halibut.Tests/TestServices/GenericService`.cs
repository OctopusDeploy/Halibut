using System.Threading;
using System.Threading.Tasks;
using Halibut.TestUtils.Contracts;

namespace Halibut.Tests.TestServices
{
    public class GenericService<T> : IGenericService<T>
    {
        public string GetInfo(T value)
        {
            return $"{typeof(T).Name} => {value}";
        }
    }

    public class AsyncGenericService<T> : IAsyncGenericService<T>
    {
        readonly GenericService<T> genericService;

        public AsyncGenericService(GenericService<T> genericService)
        {
            this.genericService = genericService;
        }

        public async Task<string> GetInfoAsync(T value, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            return genericService.GetInfo(value);
        }
    }
}