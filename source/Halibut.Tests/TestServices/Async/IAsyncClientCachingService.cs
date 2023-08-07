using System;
using System.Threading.Tasks;
using Halibut.Transport.Caching;

namespace Halibut.Tests.TestServices.Async
{
    /// <summary>
    /// Use this interface when generating the proxy client in tests.
    /// </summary>
    public interface IAsyncClientCachingService
    {
        Task<Guid> NonCachableCallAsync();

        [CacheResponse(600)]
        Task<Guid> CachableCallAsync();

        [CacheResponse(600)]
        Task<Guid> AnotherCachableCallAsync();

        [CacheResponse(600)]
        Task<Guid> CachableCallAsync(Guid input);

        [CacheResponse(600)]
        Task<Guid> CachableCallThatThrowsAnExceptionWithARandomExceptionMessageAsync(string exceptionMessagePrefix);

        [CacheResponse(2)]
        Task<Guid> TwoSecondCachableCallAsync();
    }
}
