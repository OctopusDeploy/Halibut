#if NET8_0_OR_GREATER
using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.Queue.Redis.RedisHelpers;
using Halibut.Util;

namespace Halibut.Queue.Redis.RedisDataLossDetection
{
    public class WatchForRedisLosingAllItsData : IWatchForRedisLosingAllItsData
    {
        readonly RedisFacade redisFacade;
        readonly ILog log;
        
        /// <summary>
        /// If we are yet to contact redis to watch it for data lose, this is the delay
        /// between errors used when retrying to connect to redis.
        /// </summary>
        internal TimeSpan SetupErrorBackoffDelay { get;}
        
        /// <summary>
        /// The amount of time between checks to check if redis has had data lose.
        /// </summary>
        internal TimeSpan DataLossCheckInterval { get; }
        
        /// <summary>
        /// The TTL of the key used for data lose detection. The TTL is reset
        /// each time we check for data lose. This exists so that the data is
        /// eventually removed from redis.
        /// </summary>
        internal TimeSpan DataLostKeyTtl { get;  }
        
        CancelOnDisposeCancellationToken cts = new();

        public WatchForRedisLosingAllItsData(RedisFacade redisFacade, ILog log, TimeSpan? setupDelay = null, TimeSpan? watchInterval = null, TimeSpan? keyTTL = null)
        {
            this.redisFacade = redisFacade;
            this.log = log;
            this.SetupErrorBackoffDelay = setupDelay ?? TimeSpan.FromSeconds(1);
            this.DataLossCheckInterval = watchInterval ?? TimeSpan.FromSeconds(60);
            this.DataLostKeyTtl = keyTTL ?? TimeSpan.FromHours(8);
            var _ = Task.Run(async () => await KeepWatchingForDataLoss(cts.Token));
        }

        private TaskCompletionSource<CancellationToken> taskCompletionSource = new TaskCompletionSource<CancellationToken>();
        
        /// <summary>
        /// Will cause the caller to wait until we are connected to redis and so can detect datalose.
        /// </summary>
        /// <param name="timeToWait">Time to wait for this to reach a state where it can detect datalose</param>
        /// <param name="cancellationToken"></param>
        /// <returns>A cancellation token which is triggered when data lose occurs.</returns>
        public async Task<CancellationToken> GetTokenForDataLossDetection(TimeSpan timeToWait, CancellationToken cancellationToken)
        {
            if (taskCompletionSource.Task.IsCompleted)
            {
                return await taskCompletionSource.Task;
            }
            
            await using var cts = new CancelOnDisposeCancellationToken(cancellationToken);
            cts.CancelAfter(timeToWait);
            return await taskCompletionSource.Task.WaitAsync(cts.Token);
        }

        private async Task KeepWatchingForDataLoss(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Try.IgnoringError(async () => await WatchForDataLoss(cancellationToken));
            }
        }

        async Task WatchForDataLoss(CancellationToken cancellationToken)
        {
            string guid = Guid.NewGuid().ToString();
            var key = "WatchForDataLoss::" + guid;
            var hasSetKey = false;
            
            log.Write(EventType.Diagnostic, "Starting Redis data loss monitoring with key {0}", key);
            
            await using var cts = new CancelOnDisposeCancellationToken();
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (!hasSetKey)
                    {
                        log.Write(EventType.Diagnostic, "Setting initial data loss monitoring key {0} with TTL {1} minutes", key, DataLostKeyTtl.TotalMinutes);
                        await redisFacade.SetString(key, guid, DataLostKeyTtl, cancellationToken);
                        taskCompletionSource.TrySetResult(cts.Token);
                        hasSetKey = true;
                        log.Write(EventType.Diagnostic, "Successfully set initial data loss monitoring key {0}, monitoring is now active", key);
                    }
                    else
                    {
                        var data = await redisFacade.GetString(key, cancellationToken);
                        if (data != guid)
                        {
                            log.Write(EventType.Error, "Redis data loss detected! Expected value {0} for key {1}, but got {2}. This indicates Redis has lost data.", guid, key, data ?? "null");
                            // Anyone new will be given a new thing to wait on.
                            taskCompletionSource = new TaskCompletionSource<CancellationToken>();
                            await Try.IgnoringError(async () => await cts.CancelAsync());
                            return;
                        }

                        await redisFacade.SetTtlForString(key, DataLostKeyTtl, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    log.Write(EventType.Diagnostic, "Error occurred during Redis data loss monitoring for key {0}: {1}. Will retry after delay.", key, ex.Message);
                }

                await Try.IgnoringError(async () =>
                {
                    if (!hasSetKey) await Task.Delay(SetupErrorBackoffDelay, cancellationToken);
                    else await Task.Delay(DataLossCheckInterval, cancellationToken);
                });

            }

            log.Write(EventType.Diagnostic, "Redis data loss monitoring stopped for key {0}, cleaning up", key);
            await Try.IgnoringError(async () => await redisFacade.DeleteString(key, cancellationToken));
        }

        public async ValueTask DisposeAsync()
        {
            log.Write(EventType.Diagnostic, "Disposing WatchForRedisLosingAllItsData");
            await cts.DisposeAsync();
        }
    }
}
#endif