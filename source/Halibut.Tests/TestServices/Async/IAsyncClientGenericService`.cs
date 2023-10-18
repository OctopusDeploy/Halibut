using System;
using System.Threading.Tasks;

namespace Halibut.Tests.TestServices.Async
{
    public interface IAsyncClientGenericService<in T>
    {
        Task<string> GetInfoAsync(T value);
    }
}