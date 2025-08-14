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
            IHalibutRedisTransport halibutRedisTransport, 
            Uri endpoint, 
            RequestMessage request,
            ILog log)
        {
            log.Write(EventType.Diagnostic, "Attempting to send cancellation for request - Endpoint: {0}, ActivityId: {1}", endpoint, request.ActivityId);
            
            await using var cts = new CancelOnDisposeCancellationToken();
            cts.CancelAfter(TimeSpan.FromMinutes(2)); // Best efforts.
            
            try
            {
                log.Write(EventType.Diagnostic, "Publishing cancellation notification - Endpoint: {0}, ActivityId: {1}", endpoint, request.ActivityId);
                await halibutRedisTransport.PublishCancellation(endpoint, request.ActivityId, cts.Token);
                
                log.Write(EventType.Diagnostic, "Marking request as cancelled - Endpoint: {0}, ActivityId: {1}", endpoint, request.ActivityId);
                await halibutRedisTransport.MarkRequestAsCancelled(endpoint, request.ActivityId, CancelRequestMarkerTTL, cts.Token);
                
                log.Write(EventType.Diagnostic, "Successfully sent cancellation for request - Endpoint: {0}, ActivityId: {1}", endpoint, request.ActivityId);
            }
            catch (OperationCanceledException ex)
            {
                log.Write(EventType.Error, "Cancellation send operation timed out after 2 minutes - Endpoint: {0}, ActivityId: {1}, Error: {2}", endpoint, request.ActivityId, ex.Message);
            }
            catch (Exception ex)
            {
                log.Write(EventType.Error, "Failed to send cancellation for request - Endpoint: {0}, ActivityId: {1}, Error: {2}", endpoint, request.ActivityId, ex.Message);
            }
        }

        // How long the CancelRequestMarker will sit in redis before it times out.
        // If it does timeout it won't matter since the request-sender will stop sending heart beats
        // causing the request-processor to cancel the request anyway. 
        static TimeSpan CancelRequestMarkerTTL = TimeSpan.FromMinutes(5);

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
                
                // Also poll to see if the request is cancelled since we can miss
                // the publication.
                // TODO: reconsider if we need this since the heart beats should take care of this.
                while (!token.IsCancellationRequested)
                {
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
                    await Try.IgnoringError(async () => await Task.Delay(TimeSpan.FromSeconds(60), token));
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