using System;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.TestUtils.Contracts
{
    public interface IGenericService<in T>
    {
        string GetInfo(T value);
    }

    public interface IAsyncGenericService<in T>
    {
        Task<string> GetInfoAsync(T value, CancellationToken cancellationToken);
    }
}