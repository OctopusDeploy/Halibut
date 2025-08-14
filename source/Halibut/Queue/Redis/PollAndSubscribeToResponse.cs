// Copyright 2012-2013 Octopus Deploy Pty. Ltd.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//   http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.Util;
using Nito.AsyncEx;

namespace Halibut.Queue.Redis
{
    public class PollAndSubscribeToResponse : IAsyncDisposable
    {
        public static async Task TrySendMessage(
            IHalibutRedisTransport halibutRedisTransport, 
            Uri endpoint, 
            Guid activityId,
            string value,
            TimeSpan ttl,
            ILog log)
        {
            log.Write(EventType.Diagnostic, "Attempting to set response for - Endpoint: {0}, ActivityId: {1}", endpoint, activityId);
            
            await using var cts = new CancelOnDisposeCancellationToken();
            cts.CancelAfter(TimeSpan.FromMinutes(2)); // Best efforts.
            
            try
            {
                log.Write(EventType.Diagnostic, "Marking response as set - Endpoint: {0}, ActivityId: {1}", endpoint, activityId);
                await halibutRedisTransport.MarkThatResponseIsSet(endpoint, activityId, value, ttl, cts.Token);
                
                log.Write(EventType.Diagnostic, "Publishing response notification - Endpoint: {0}, ActivityId: {1}", endpoint, activityId);
                await halibutRedisTransport.PublishThatResponseIsAvailable(endpoint, activityId, value, cts.Token);
                
                log.Write(EventType.Diagnostic, "Successfully set response - Endpoint: {0}, ActivityId: {1}", endpoint, activityId);
            }
            catch (OperationCanceledException ex)
            {
                log.Write(EventType.Error, "Set response operation timed out after 2 minutes - Endpoint: {0}, ActivityId: {1}, Error: {2}", endpoint, activityId, ex.Message);
            }
            catch (Exception ex)
            {
                log.Write(EventType.Error, "Failed to set response - Endpoint: {0}, ActivityId: {1}, Error: {2}", endpoint, activityId, ex.Message);
            }
        }

        readonly CancelOnDisposeCancellationToken watcherToken;

        readonly ILog log;

        readonly IHalibutRedisTransport halibutRedisTransport;
        readonly Uri endpoint;
        readonly Guid activityId;
        readonly LinearBackoffStrategy pollBackoffStrategy;

        TaskCompletionSource<string> ResponseJsonCompletionSource = new();
        
        public Task<string> ResponseJson => ResponseJsonCompletionSource.Task;

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

            watcherToken = new CancelOnDisposeCancellationToken();
            var token = watcherToken.Token;
            watcherToken.AwaitTasksBeforeCTSDispose(Task.Run(async () => await WaitForResponse(token)));
        }

        readonly SemaphoreSlim trySetResultSemaphore = new SemaphoreSlim(1, 1);

        async Task WaitForResponse(CancellationToken token)
        {
            try
            {
                log.Write(EventType.Diagnostic, "Subscribing to response notifications - Endpoint: {0}, ActivityId: {1}", endpoint, activityId);
                
                await using var _ = await halibutRedisTransport.SubscribeToResponseChannel(endpoint, activityId,
                    async _ =>
                    {
                        
                        log.Write(EventType.Diagnostic, "Received response notification via subscription - Endpoint: {0}, ActivityId: {1}", endpoint, activityId);
                        await TryGetResponseFromRedis("subscription", token);
                    },
                    token);
                
                log.Write(EventType.Diagnostic, "Starting polling loop for response - Endpoint: {0}, ActivityId: {1}", endpoint, activityId);
                
                // Also poll to see if the value is set since we can miss
                // the publication.
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

        /// <summary>
        /// Makes an attempt to get the response from redis.
        /// </summary>
        /// <param name="detectedBy"></param>
        /// <param name="token"></param>
        /// <returns>true if a response message is available.</returns>
        public async Task<bool> TryGetResponseFromRedis(string detectedBy, CancellationToken token)
        {
            using var l = await trySetResultSemaphore.LockAsync(token);
            
            if (ResponseJsonCompletionSource.Task.IsCompleted) return true;
            
            var responseJson = await halibutRedisTransport.GetResponseMessage(endpoint, activityId, token);
            if (responseJson != null)
            {
                log.Write(EventType.Diagnostic, "Response detected  via {0} - Endpoint: {1}, ActivityId: {2}", detectedBy, endpoint, activityId);
                await TrySetResponse(responseJson, token);
                await Try.IgnoringError(async () => await watcherToken.CancelAsync());
                log.Write(EventType.Diagnostic, "Cancelling  polling loop for response - Endpoint: {0}, ActivityId: {1}", endpoint, activityId);
                return true;
            }

            return false;
        }
        
        async Task TrySetResponse(string value, CancellationToken cancellationToken)
        {
            try
            {
                ResponseJsonCompletionSource.TrySetResult(value);
            }
            catch (Exception ex)
            {
                log.Write(EventType.Error, "Failed to set response - Endpoint: {0}, ActivityId: {1}, Error: {2}", endpoint, activityId, ex.Message);
            }

            try
            {
                await halibutRedisTransport.DeleteResponse(endpoint, activityId, cancellationToken);
            }
            catch (Exception ex)
            {
                log.Write(EventType.Error, "Failed to delete response from Redis - Endpoint: {0}, ActivityId: {1}, Error: {2}", endpoint, activityId, ex.Message);
            }

        }

        public async ValueTask DisposeAsync()
        {
            log.Write(EventType.Diagnostic, "Disposing GenericWatcher for response - Endpoint: {0}, ActivityId: {1}", endpoint, activityId);
            
            await Try.IgnoringError(async () => await watcherToken.CancelAsync());
            
            // If the message task is not yet complete, then mark it as cancelled
            Try.IgnoringError(() => ResponseJsonCompletionSource.TrySetCanceled());
            
            log.Write(EventType.Diagnostic, "Disposed GenericWatcher for response - Endpoint: {0}, ActivityId: {1}", endpoint, activityId);
        }
    }
} 