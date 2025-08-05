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
    public class PollAndSubscribeForSingleMessage : IAsyncDisposable
    {
        public static async Task TrySendMessage(
            string messageTypeName,
            HalibutRedisTransport halibutRedisTransport, 
            Uri endpoint, 
            Guid activityId,
            string value,
            ILog log)
        {
            log.Write(EventType.Diagnostic, "Attempting to set {0} for - Endpoint: {1}, ActivityId: {2}", messageTypeName, endpoint, activityId);
            
            using var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMinutes(2)); // Best efforts.
            
            try
            {
                log.Write(EventType.Diagnostic, "Marking {0} as set - Endpoint: {1}, ActivityId: {2}", messageTypeName, endpoint, activityId);
                await halibutRedisTransport.SendValue(messageTypeName, endpoint, activityId, value, cts.Token);
                
                log.Write(EventType.Diagnostic, "Publishing {0} notification - Endpoint: {1}, ActivityId: {2}", messageTypeName, endpoint, activityId);
                await halibutRedisTransport.PublishThatValueIsAvailable(messageTypeName, endpoint, activityId, value, cts.Token);
                
                log.Write(EventType.Diagnostic, "Successfully set {0} - Endpoint: {1}, ActivityId: {2}", messageTypeName, endpoint, activityId);
            }
            catch (OperationCanceledException ex)
            {
                log.Write(EventType.Error, "Set {0} operation timed out after 2 minutes - Endpoint: {1}, ActivityId: {2}, Error: {3}", messageTypeName, endpoint, activityId, ex.Message);
            }
            catch (Exception ex)
            {
                log.Write(EventType.Error, "Failed to set {0} - Endpoint: {1}, ActivityId: {2}, Error: {3}", messageTypeName, endpoint, activityId, ex.Message);
            }
        }

        readonly CancellationTokenSource watcherTokenSource = new();

        readonly ILog log;
        readonly string messageTypeName;
        readonly HalibutRedisTransport halibutRedisTransport;
        readonly Uri endpoint;
        readonly Guid activityId;
        readonly LinearBackoffStrategy pollBackoffStrategy;

        TaskCompletionSource<string> message = new();
        
        public Task<string> ResultTask => message.Task;

        public PollAndSubscribeForSingleMessage(string messageTypeName, Uri endpoint, Guid activityId, HalibutRedisTransport halibutRedisTransport, ILog log)
        {
            this.log = log;
            this.messageTypeName = messageTypeName;
            this.endpoint = endpoint;
            this.activityId = activityId;
            this.halibutRedisTransport = halibutRedisTransport;
            this.pollBackoffStrategy = new LinearBackoffStrategy(
                TimeSpan.FromSeconds(15),   // Initial delay: 15s
                TimeSpan.FromSeconds(15),   // Increment: 15s
                TimeSpan.FromMinutes(2)     // Maximum delay: 2 minutes
            );
            log.Write(EventType.Diagnostic, "Starting to watch for {0} - Endpoint: {1}, ActivityId: {2}", messageTypeName, endpoint, activityId);
            
            var token = watcherTokenSource.Token;
            var _ = Task.Run(async () => await WatchAndWaitForMessage(token));
        }

        readonly SemaphoreSlim trySetResultSemaphore = new SemaphoreSlim(1, 1);

        public async Task TrySetResultAndRemoveValueFromRedis(string value, CancellationToken cancellationToken)
        {
            using var l = await trySetResultSemaphore.LockAsync(cancellationToken);
            try
            {
                if(!message.Task.IsCompleted) message.TrySetResult(value);
            }
            catch (Exception)
            {
                // TODO log we could not set result.
            }

            try
            {
                await halibutRedisTransport.DeleteGenericMarker(messageTypeName, endpoint, activityId, cancellationToken);
            }
            catch (Exception)
            {
                // TODO log we could not remove value
            }

        }
        async Task WatchAndWaitForMessage(CancellationToken token)
        {
            try
            {
                log.Write(EventType.Diagnostic, "Subscribing to {0} notifications - Endpoint: {1}, ActivityId: {2}", messageTypeName, endpoint, activityId);
                
                await using var _ = await halibutRedisTransport.SubscribeToGenericNotification(messageTypeName, endpoint, activityId,
                    async _ =>
                    {
                        
                        log.Write(EventType.Diagnostic, "Received {0} notification via subscription - Endpoint: {1}, ActivityId: {2}", messageTypeName, endpoint, activityId);
                        
                        var value = await halibutRedisTransport.GetGenericMarker(messageTypeName, endpoint, activityId, token);
                        if (value != null)
                        {
                            await TrySetResultAndRemoveValueFromRedis(value, token);
                        }
                        
                        await watcherTokenSource.CancelAsync();
                    },
                    token);
                
                log.Write(EventType.Diagnostic, "Starting polling loop for {0} - Endpoint: {1}, ActivityId: {2}", messageTypeName, endpoint, activityId);
                
                // Also poll to see if the value is set since we can miss
                // the publication.
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        var value = await halibutRedisTransport.GetGenericMarker(messageTypeName, endpoint, activityId, token);
                        if (value != null)
                        {
                            log.Write(EventType.Diagnostic, "{0} detected via polling - Endpoint: {1}, ActivityId: {2}", messageTypeName, endpoint, activityId);
                            pollBackoffStrategy.Success(); // Reset backoff strategy on successful retrieval
                            await TrySetResultAndRemoveValueFromRedis(value, token);
                            await watcherTokenSource.CancelAsync();
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Write(EventType.Diagnostic, "Error while polling for {0} - Endpoint: {1}, ActivityId: {2}, Error: {3}", messageTypeName, endpoint, activityId, ex.Message);
                    }
                    
                    pollBackoffStrategy.Try();
                    var delay = pollBackoffStrategy.GetSleepPeriod();
                    log.Write(EventType.Diagnostic, "Waiting {0} seconds before next poll for {1} - Endpoint: {2}, ActivityId: {3}", delay.TotalSeconds, messageTypeName, endpoint, activityId);
                    await Task.Delay(delay, token);
                }
                
                log.Write(EventType.Diagnostic, "Exiting watch loop for {0} - Endpoint: {1}, ActivityId: {2}", messageTypeName, endpoint, activityId);
            }
            catch (Exception ex)
            {
                log.Write(EventType.Error, "Unexpected error in {0} watcher - Endpoint: {1}, ActivityId: {2}, Error: {3}", messageTypeName, endpoint, activityId, ex.Message);
            }
        }

        public async ValueTask DisposeAsync()
        {
            log.Write(EventType.Diagnostic, "Disposing GenericWatcher for {0}", messageTypeName);
            
            await Try.IgnoringError(async () => await watcherTokenSource.CancelAsync());
            Try.IgnoringError(() => watcherTokenSource.Dispose());
            
            // If the message task is not yet complete, then complete if now with null since we have nothing for it.
            Try.IgnoringError(() => message.TrySetCanceled());
            
            log.Write(EventType.Diagnostic, "GenericWatcher for {0} disposed", messageTypeName);
        }
    }
} 