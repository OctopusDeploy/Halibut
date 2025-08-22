using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Queue.Redis;
using Halibut.Queue.Redis.RedisDataLossDetection;
using Halibut.Util;
using Try = Halibut.Tests.Support.Try;

namespace Halibut.Tests.Queue.Redis.Utils
{
    public class CancellableDataLossWatchForRedisLosingAllItsData : IWatchForRedisLosingAllItsData
    {
        CancelOnDisposeCancellationToken cancellationToken = new();

        public TaskCompletionSource<CancellationToken> TaskCompletionSource = new();
        public CancellableDataLossWatchForRedisLosingAllItsData()
        {
            TaskCompletionSource.SetResult(cancellationToken.Token);
        }

        public async Task DataLossHasOccured()
        {
            await cancellationToken.DisposeAsync();
            cancellationToken = new CancelOnDisposeCancellationToken();
            TaskCompletionSource = new TaskCompletionSource<CancellationToken>();
            TaskCompletionSource.SetResult(cancellationToken.Token);
        }

        public async ValueTask DisposeAsync()
        {
            await Try.CatchingError(async () => await cancellationToken.DisposeAsync());
        }

        public async Task<CancellationToken> GetTokenForDataLossDetection(TimeSpan timeToWait, CancellationToken cancellationToken)
        {
#pragma warning disable VSTHRD003
            return await TaskCompletionSource.Task;
#pragma warning restore VSTHRD003
        }
    }
}