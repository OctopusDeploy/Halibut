
#if NET8_0_OR_GREATER
using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.Util;
using Nito.AsyncEx;

namespace Halibut.Queue.Redis.ResponseMessageTransfer
{
    public class PollAndSubscribeToResponse : IAsyncDisposable
    {
        readonly CancelOnDisposeCancellationToken objectLifeTimeCts;
        readonly ILog log;
        readonly IHalibutRedisTransport halibutRedisTransport;
        readonly Uri endpoint;
        readonly Guid activityId;
        readonly LinearBackoffStrategy pollBackoffStrategy;

        readonly TaskCompletionSource<string> responseJsonCompletionSource = new();
        
        /// <summary>
        /// An awaitable task that returns when the response is available.
        /// </summary>
        public Task<string> ResponseJson => responseJsonCompletionSource.Task;

        public PollAndSubscribeToResponse(Uri endpoint, Guid activityId, IHalibutRedisTransport halibutRedisTransport, ILog log)
        {
            this.log = log.ForContext<PollAndSubscribeToResponse>();

            this.endpoint = endpoint;
            this.activityId = activityId;
            this.halibutRedisTransport = halibutRedisTransport;
            this.pollBackoffStrategy = new LinearBackoffStrategy(
                TimeSpan.FromSeconds(15),   // Initial delay: 15s
                TimeSpan.FromSeconds(15),   // Increment: 15s
                TimeSpan.FromMinutes(2)     // Maximum delay: 2 minutes
            );
            this.log.Write(EventType.Diagnostic, "Starting to watch for response - Endpoint: {0}, ActivityId: {1}", endpoint, activityId);

            objectLifeTimeCts = new CancelOnDisposeCancellationToken();
            var token = objectLifeTimeCts.Token;
            objectLifeTimeCts.AwaitTasksBeforeCTSDispose(Task.Run(async () => await WaitForResponse(token)));
        }

        async Task WaitForResponse(CancellationToken token)
        {
            try
            {
                log.Write(EventType.Diagnostic, "Subscribing to response notifications - Endpoint: {0}, ActivityId: {1}", endpoint, activityId);
                
                // This could wait forever to subscribe to redis if redis is offline. We need some way of limiting how long we take
                // to subscribe.
                // https://whimsical.com/subscribetonodeheartbeatchannel-should-timeout-while-waiting-to--NFWwmPkE7pTBdm2PRUC8Tf
                await using var _ = await halibutRedisTransport.SubscribeToResponseChannel(endpoint, activityId,
                    async _ =>
                    {
                        
                        log.Write(EventType.Diagnostic, "Received response notification via subscription - Endpoint: {0}, ActivityId: {1}", endpoint, activityId);
                        await TryGetResponseFromRedis("subscription", token);
                    },
                    token);
                
                log.Write(EventType.Diagnostic, "Starting polling loop for response - Endpoint: {0}, ActivityId: {1}", endpoint, activityId);
                
                // Also poll to see if the value is set since we can miss the publication.
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        if (await TryGetResponseFromRedis("polling", token))
                        {
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Write(EventType.Diagnostic, "Error while polling for response - Endpoint: {0}, ActivityId: {1}, Error: {2}", endpoint, activityId, ex.Message);
                    }
                    
                    pollBackoffStrategy.Try();
                    var delay = pollBackoffStrategy.GetSleepPeriod();
                    log.Write(EventType.Diagnostic, "Waiting {0} seconds before next poll for response - Endpoint: {1}, ActivityId: {2}", delay.TotalSeconds, endpoint, activityId);
                    await Try.IgnoringError(async () => await Task.Delay(delay, token));
                    log.Write(EventType.Diagnostic, "Done waiting going around the loop response - Endpoint: {0}, ActivityId: {1}", endpoint, activityId);
                }
                
                log.Write(EventType.Diagnostic, "Exiting watch loop for response - Endpoint: {0}, ActivityId: {1}", endpoint, activityId);
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                {
                    log.Write(EventType.Error, "Unexpected error in response watcher - Endpoint: {0}, ActivityId: {1}, Error: {2}", endpoint, activityId, ex.Message);
                }
            }
        }
        
        readonly SemaphoreSlim trySetResultSemaphore = new(1, 1);

        /// <summary>
        /// Makes an attempt to get the response from redis.
        /// </summary>
        /// <param name="detectedBy"></param>
        /// <param name="token"></param>
        /// <returns>true if a response message is available.</returns>
        public async Task<bool> TryGetResponseFromRedis(string detectedBy, CancellationToken token)
        {
            using var l = await trySetResultSemaphore.LockAsync(token);
            
            if (responseJsonCompletionSource.Task.IsCompleted) return true;
            
            var responseJson = await halibutRedisTransport.GetResponseMessage(endpoint, activityId, token);
            
            if (responseJson != null)
            {
                log.Write(EventType.Diagnostic, "Response detected via {0} - Endpoint: {1}, ActivityId: {2}", detectedBy, endpoint, activityId);
                
                await DeleteResponseFromRedis(detectedBy, token);
                
                TrySetResponse(responseJson);
                await Try.IgnoringError(async () => await objectLifeTimeCts.CancelAsync());
                log.Write(EventType.Diagnostic, "Cancelling  polling loop for response - Endpoint: {0}, ActivityId: {1}", endpoint, activityId);
                return true;
            }

            return false;
        }

        async Task DeleteResponseFromRedis(string detectedBy, CancellationToken token)
        {
            try
            {
                await halibutRedisTransport.DeleteResponseMessage(endpoint, activityId, token);
            }
            catch (Exception ex)
            {
                log.Write(EventType.Error, "Failed to delete response from Redis via {0} - Endpoint: {1}, ActivityId: {2}, Error: {3}", detectedBy, endpoint, activityId, ex.Message);
            }
        }

        void TrySetResponse(string value)
        {
            try
            {
                responseJsonCompletionSource.TrySetResult(value);
            }
            catch (Exception ex)
            {
                log.Write(EventType.Error, "Failed to set response - Endpoint: {0}, ActivityId: {1}, Error: {2}", endpoint, activityId, ex.Message);
            }
        }

        public async ValueTask DisposeAsync()
        {
            log.Write(EventType.Diagnostic, "Disposing GenericWatcher for response - Endpoint: {0}, ActivityId: {1}", endpoint, activityId);
            
            await Try.IgnoringError(async () => await objectLifeTimeCts.CancelAsync());
            
            // If the message task is not yet complete, then mark it as cancelled
            Try.IgnoringError(() => responseJsonCompletionSource.TrySetCanceled());
            
            log.Write(EventType.Diagnostic, "Disposed GenericWatcher for response - Endpoint: {0}, ActivityId: {1}", endpoint, activityId);
        }
    }
} 
#endif