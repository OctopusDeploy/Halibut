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
using Halibut.Transport.Protocol;
using Halibut.Util;

namespace Halibut.Queue.Redis
{
    public class WatchForRequestCancellation : IAsyncDisposable
    {
        
        public static async Task TrySendCancellation(
            HalibutRedisTransport halibutRedisTransport, 
            Uri endpoint, 
            RequestMessage request,
            ILog log)
        {
            log.Write(EventType.Diagnostic, $"Attempting to send cancellation for request - Endpoint: {endpoint}, ActivityId: {request.ActivityId}");
            
            using var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMinutes(2)); // Best efforts.
            
            try
            {
                log.Write(EventType.Diagnostic, $"Publishing cancellation notification - Endpoint: {endpoint}, ActivityId: {request.ActivityId}");
                await halibutRedisTransport.PublishCancellation(endpoint, request.ActivityId, cts.Token);
                
                log.Write(EventType.Diagnostic, $"Marking request as cancelled - Endpoint: {endpoint}, ActivityId: {request.ActivityId}");
                await halibutRedisTransport.MarkRequestAsCancelled(endpoint, request.ActivityId, cts.Token);
                
                log.Write(EventType.Diagnostic, $"Successfully sent cancellation for request - Endpoint: {endpoint}, ActivityId: {request.ActivityId}");
            }
            catch (OperationCanceledException ex)
            {
                log.Write(EventType.Error, $"Cancellation send operation timed out after 2 minutes - Endpoint: {endpoint}, ActivityId: {request.ActivityId}, Error: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                log.Write(EventType.Error, $"Failed to send cancellation for request - Endpoint: {endpoint}, ActivityId: {request.ActivityId}, Error: {ex.Message}");
                throw;
            }
        }
        readonly CancellationTokenSource requestCancelledCts = new();
        public CancellationToken RequestCancelledCancellationToken => requestCancelledCts.Token;

        readonly CancellationTokenSource watchForCancellationTokenSource = new();

        readonly ILog log;

        public WatchForRequestCancellation(Uri endpoint, Guid requestActivityId, HalibutRedisTransport halibutRedisTransport, ILog log)
        {
            this.log = log;
            log.Write(EventType.Diagnostic, $"Starting to watch for request cancellation - Endpoint: {endpoint}, ActivityId: {requestActivityId}");
            
            var token = watchForCancellationTokenSource.Token;
            var _ = Task.Run(async () => await WatchForExceptions(endpoint, requestActivityId, halibutRedisTransport, token));
        }

        async Task WatchForExceptions(Uri endpoint, Guid requestActivityId, HalibutRedisTransport halibutRedisTransport, CancellationToken token)
        {
            try
            {
                log.Write(EventType.Diagnostic, $"Subscribing to request cancellation notifications - Endpoint: {endpoint}, ActivityId: {requestActivityId}");
                
                await using var _ = await halibutRedisTransport.SubscribeToRequestCancellation(endpoint, requestActivityId,
                    async () =>
                    {
                        await Task.CompletedTask;
                        log.Write(EventType.Diagnostic, $"Received cancellation notification via subscription - Endpoint: {endpoint}, ActivityId: {requestActivityId}");
                        await requestCancelledCts.CancelAsync();
                        await watchForCancellationTokenSource.CancelAsync();
                    },
                    token);
                
                log.Write(EventType.Diagnostic, $"Starting polling loop for request cancellation - Endpoint: {endpoint}, ActivityId: {requestActivityId}");
                
                // Also poll to see if the request is cancelled since we can miss
                // the publication.
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        if (await halibutRedisTransport.IsRequestMarkedAsCancelled(endpoint, requestActivityId, token))
                        {
                            log.Write(EventType.Diagnostic, $"Request cancellation detected via polling - Endpoint: {endpoint}, ActivityId: {requestActivityId}");
                            await requestCancelledCts.CancelAsync();
                            await watchForCancellationTokenSource.CancelAsync();
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Write(EventType.Diagnostic, $"Error while polling for request cancellation - Endpoint: {endpoint}, ActivityId: {requestActivityId}, Error: {ex.Message}");
                    }
                    await Task.Delay(TimeSpan.FromSeconds(60), token);
                }
                
                log.Write(EventType.Diagnostic, $"Exiting watch loop for request cancellation - Endpoint: {endpoint}, ActivityId: {requestActivityId}");
            }
            catch (Exception ex)
            {
                log.Write(EventType.Error, $"Unexpected error in request cancellation watcher - Endpoint: {endpoint}, ActivityId: {requestActivityId}, Error: {ex.Message}");
            }
        }

        public async ValueTask DisposeAsync()
        {
            log.Write(EventType.Diagnostic, "Disposing WatchForRequestCancellation");
            
            await Try.IgnoringError(async () => await watchForCancellationTokenSource.CancelAsync());
            Try.IgnoringError(() => watchForCancellationTokenSource.Dispose());
            await Try.IgnoringError(async () => await requestCancelledCts.CancelAsync());
            Try.IgnoringError(() => requestCancelledCts.Dispose());
            
            log.Write(EventType.Diagnostic, "WatchForRequestCancellation disposed");
        }
    }
}