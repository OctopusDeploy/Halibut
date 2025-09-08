
#if NET8_0_OR_GREATER
using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.Queue.Redis.Cancellation;
using Halibut.Queue.Redis.NodeHeartBeat;
using Halibut.Queue.Redis.RedisHelpers;
using Halibut.Util;

namespace Halibut.Queue.Redis
{
    
    public class WatchForRequestCancellationOrSenderDisconnect : IAsyncDisposable
    {
        readonly CancelOnDisposeCancellationToken requestCancellationTokenSource;
        public CancellationToken RequestProcessingCancellationToken { get; }

        readonly CancelOnDisposeCancellationToken keepWatchingCancellationToken;

        readonly DisposableCollection disposableCollection = new();

        readonly WatchForRequestCancellation watchForRequestCancellation;
        public bool SenderCancelledTheRequest => watchForRequestCancellation.SenderCancelledTheRequest;

        public WatchForRequestCancellationOrSenderDisconnect(
            Uri endpoint,
            Guid requestActivityId,
            IHalibutRedisTransport halibutRedisTransport,
            TimeSpan nodeOfflineTimeoutBetweenHeartBeatsFromSender,
            ILog log)
        {
            try
            {
                watchForRequestCancellation = new WatchForRequestCancellation(endpoint, requestActivityId, halibutRedisTransport, log);
                disposableCollection.AddAsyncDisposable(watchForRequestCancellation);

                requestCancellationTokenSource = new CancelOnDisposeCancellationToken(watchForRequestCancellation.RequestCancelledCancellationToken);
                disposableCollection.AddAsyncDisposable(requestCancellationTokenSource);
                RequestProcessingCancellationToken = requestCancellationTokenSource.Token;

                keepWatchingCancellationToken = new CancelOnDisposeCancellationToken();
                disposableCollection.AddAsyncDisposable(keepWatchingCancellationToken);

                Task.Run(() => WatchThatNodeWhichSentTheRequestIsStillAlive(endpoint, requestActivityId, halibutRedisTransport, nodeOfflineTimeoutBetweenHeartBeatsFromSender, log));
            }
            catch (Exception)
            {
                Try.IgnoringError(async () => await disposableCollection.DisposeAsync()).GetAwaiter().GetResult();
                throw;
            }
        }

        async Task WatchThatNodeWhichSentTheRequestIsStillAlive(Uri endpoint, Guid requestActivityId, IHalibutRedisTransport halibutRedisTransport, TimeSpan nodeOfflineTimeoutBetweenHeartBeatsFromSender, ILog log)
        {
            var watchCancellationToken = keepWatchingCancellationToken.Token;
            try
            {
                var res = await NodeHeartBeatWatcher
                    .WatchThatNodeWhichSentTheRequestIsStillAlive(endpoint, requestActivityId, halibutRedisTransport, log, nodeOfflineTimeoutBetweenHeartBeatsFromSender, watchCancellationToken);
                if (res == NodeWatcherResult.NodeMayHaveDisconnected)
                {
                    await requestCancellationTokenSource.CancelAsync();
                }
            }
            catch (Exception) when (watchCancellationToken.IsCancellationRequested)
            {
                log.Write(EventType.Diagnostic, "Sender node watcher cancelled for request {0}, endpoint {1}", requestActivityId, endpoint);
            }
            catch (Exception ex)
            {
                log.WriteException(EventType.Error, "Error watching sender node for request {0}, endpoint {1}", ex, requestActivityId, endpoint);
            }
        }

        public async ValueTask DisposeAsync()
        {
            await disposableCollection.DisposeAsync();
        }
    }
}
#endif