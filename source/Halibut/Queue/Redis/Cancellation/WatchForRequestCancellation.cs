
#if NET8_0_OR_GREATER
using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.Transport.Protocol;
using Halibut.Util;

namespace Halibut.Queue.Redis.Cancellation
{
    public class WatchForRequestCancellation : IAsyncDisposable
    {
        readonly CancelOnDisposeCancellationToken requestCancelledCts = new();
        public CancellationToken RequestCancelledCancellationToken => requestCancelledCts.Token;
        public bool SenderCancelledTheRequest { get; private set; }

        readonly CancelOnDisposeCancellationToken watchForCancellationTokenSource = new();

        readonly ILog log;

        public WatchForRequestCancellation(Uri endpoint, Guid requestActivityId, IHalibutRedisTransport halibutRedisTransport, ILog log)
        {
            this.log = log;
            log.Write(EventType.Diagnostic, "Starting to watch for request cancellation - Endpoint: {0}, ActivityId: {1}", endpoint, requestActivityId);
            
            var token = watchForCancellationTokenSource.Token;
            var _ = Task.Run(async () => await WatchForCancellation(endpoint, requestActivityId, halibutRedisTransport, token));
        }

        async Task WatchForCancellation(Uri endpoint, Guid requestActivityId, IHalibutRedisTransport halibutRedisTransport, CancellationToken token)
        {
            try
            {
                log.Write(EventType.Diagnostic, "Subscribing to request cancellation notifications - Endpoint: {0}, ActivityId: {1}", endpoint, requestActivityId);
                
                await using var _ = await halibutRedisTransport.SubscribeToRequestCancellation(endpoint, requestActivityId,
                    async () =>
                    {
                        await Task.CompletedTask;
                        log.Write(EventType.Diagnostic, "Received cancellation notification via subscription - Endpoint: {0}, ActivityId: {1}", endpoint, requestActivityId);
                        await RequestHasBeenCancelled();
                    },
                    token);
                
                log.Write(EventType.Diagnostic, "Starting polling loop for request cancellation - Endpoint: {0}, ActivityId: {1}", endpoint, requestActivityId);
                
                // Also poll to see if the request is cancelled since we can miss the publication.
                while (!token.IsCancellationRequested)
                {
                    await Try.IgnoringError(async () => await Task.Delay(TimeSpan.FromSeconds(60), token));
                    
                    if(token.IsCancellationRequested) return;
                    
                    try
                    {
                        if (await halibutRedisTransport.IsRequestMarkedAsCancelled(endpoint, requestActivityId, token))
                        {
                            log.Write(EventType.Diagnostic, "Request cancellation detected via polling - Endpoint: {0}, ActivityId: {1}", endpoint, requestActivityId);
                            await RequestHasBeenCancelled();
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Write(EventType.Diagnostic, "Error while polling for request cancellation - Endpoint: {0}, ActivityId: {1}, Error: {2}", endpoint, requestActivityId, ex.Message);
                    }
                }
                
                log.Write(EventType.Diagnostic, "Exiting watch loop for request cancellation - Endpoint: {0}, ActivityId: {1}", endpoint, requestActivityId);
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                {
                    log.Write(EventType.Error, "Unexpected error in request cancellation watcher - Endpoint: {0}, ActivityId: {1}, Error: {2}", endpoint, requestActivityId, ex.Message);
                }
            }
        }

        async Task RequestHasBeenCancelled()
        {
            SenderCancelledTheRequest = true;
            await requestCancelledCts.CancelAsync();
            await watchForCancellationTokenSource.CancelAsync();
        }

        public async ValueTask DisposeAsync()
        {
            log.Write(EventType.Diagnostic, "Disposing WatchForRequestCancellation");
            
            await Try.IgnoringError(async () => await watchForCancellationTokenSource.DisposeAsync());
            await Try.IgnoringError(async () => await requestCancelledCts.DisposeAsync());
            
            log.Write(EventType.Diagnostic, "WatchForRequestCancellation disposed");
        }
    }
}
#endif