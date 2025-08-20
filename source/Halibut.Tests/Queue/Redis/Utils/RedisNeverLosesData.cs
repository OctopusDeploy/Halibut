
#if NET8_0_OR_GREATER
using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Queue.Redis;
using Halibut.Queue.Redis.RedisDataLoseDetection;

namespace Halibut.Tests.Queue.Redis.Utils
{
    /// <summary>
    /// Test implementation of IWatchForRedisLosingAllItsData that returns CancellationToken.None
    /// to indicate no data loss detection is active during testing.
    /// </summary>
    public class RedisNeverLosesData : IWatchForRedisLosingAllItsData
    {
        public Task<CancellationToken> GetTokenForDataLossDetection(TimeSpan timeToWait, CancellationToken cancellationToken)
        {
            return Task.FromResult(CancellationToken.None);
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
#endif