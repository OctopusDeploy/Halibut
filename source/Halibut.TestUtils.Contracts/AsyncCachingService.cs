using System;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.TestUtils.Contracts
{
    public class AsyncCachingService : IAsyncCachingService
    {
        public async Task<Guid> NonCachableCallAsync(CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            return Guid.NewGuid();
        }

        public async Task<Guid> CachableCallAsync(CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            return Guid.NewGuid();
        }

        public async Task<Guid> AnotherCachableCallAsync(CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            return Guid.NewGuid();
        }

        public async Task<Guid> CachableCallAsync(Guid input, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            return Guid.NewGuid();
        }

        public async Task<Guid> CachableCallThatThrowsAnExceptionWithARandomExceptionMessageAsync(string exceptionMessagePrefix, CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            throw new Exception(exceptionMessagePrefix + " " + Guid.NewGuid());
        }

        public async Task<Guid> TwoSecondCachableCallAsync(CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            return Guid.NewGuid();
        }
    }
}
