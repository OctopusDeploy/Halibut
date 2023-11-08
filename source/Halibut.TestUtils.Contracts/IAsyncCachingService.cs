using System;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.TestUtils.Contracts
{
    /// <summary>
    /// Don't use this interface to resolve a client proxy within a test since it does not have the cache attributes.
    ///
    /// This should only be used as the implemented interface.
    /// </summary>
    public interface IAsyncCachingService
    {
        Task<Guid> NonCachableCallAsync(CancellationToken cancellationToken);
        Task<Guid> CachableCallAsync(CancellationToken cancellationToken);
        Task<Guid> AnotherCachableCallAsync(CancellationToken cancellationToken);
        Task<Guid> CachableCallAsync(Guid guid, CancellationToken cancellationToken);
        Task<Guid> CachableCallThatThrowsAnExceptionWithARandomExceptionMessageAsync(string exceptionMessagePrefix, CancellationToken cancellationToken);
        Task<Guid> TwoSecondCachableCallAsync(CancellationToken cancellationToken);
    }
}