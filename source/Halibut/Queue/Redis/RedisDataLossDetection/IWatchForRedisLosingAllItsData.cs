using System;
using System.Threading;
using System.Threading.Tasks;

namespace Halibut.Queue.Redis.RedisDataLossDetection
{
    public interface IWatchForRedisLosingAllItsData : IAsyncDisposable
    {
        /// <summary>
        /// Returns a Cancellation token which is triggered when data loss occurs.
        /// Will cause the caller to wait until we are connected to redis and so can detect data loss.
        /// </summary>
        /// <param name="timeToWait">Time to wait for this to reach a state where it can detect data loss</param>
        /// <param name="cancellationToken"></param>
        /// <returns>A cancellation token which is triggered when data loss occurs.</returns>
        Task<CancellationToken> GetTokenForDataLossDetection(TimeSpan timeToWait, CancellationToken cancellationToken);
        
        /// <summary>
        /// Tries to get a cancellation token for data loss detection if monitoring is already active.
        /// This method returns immediately without waiting.
        /// </summary>
        /// <returns>A cancellation token if monitoring is active, null if monitoring is not yet ready.</returns>
        CancellationToken? TryGetTokenForDataLossDetection();
    }
}