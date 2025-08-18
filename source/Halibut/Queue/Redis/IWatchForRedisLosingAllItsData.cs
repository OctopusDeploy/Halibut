using System;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Queue.Redis
{
    public interface IWatchForRedisLosingAllItsData : IAsyncDisposable
    {
        /// <summary>
        /// Will cause the caller to wait until we are connected to redis and so can detect datalose.
        /// </summary>
        /// <param name="timeToWait">Time to wait for this to reach a state where it can detect datalose</param>
        /// <param name="cancellationToken"></param>
        /// <returns>A cancellation token which is triggered when data lose occurs.</returns>
        Task<CancellationToken> GetTokenForDataLoseDetection(TimeSpan timeToWait, CancellationToken cancellationToken);
    }
}