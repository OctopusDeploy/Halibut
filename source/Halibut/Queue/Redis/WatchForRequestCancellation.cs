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
using Halibut.Transport.Protocol;
using Halibut.Util;

namespace Halibut.Queue.Redis
{
    public class WatchForRequestCancellation : IAsyncDisposable
    {
        
        public static async Task TrySendCancellation(
            HalibutRedisTransport halibutRedisTransport, 
            Uri endpoint, 
            RequestMessage request)
        {
            using var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMinutes(2)); // Best efforts.
            await halibutRedisTransport.PublishCancellation(endpoint, request.ActivityId, cts.Token);
            await halibutRedisTransport.MarkRequestAsCancelled(endpoint, request.ActivityId, cts.Token);
        }
        readonly CancellationTokenSource requestCancelledCts = new();
        public CancellationToken RequestCancelledCancellationToken => requestCancelledCts.Token;

        readonly CancellationTokenSource watchForCancellationTokenSource = new();

        Task watchTask;

        public WatchForRequestCancellation(Uri endpoint, Guid requestActivityId, HalibutRedisTransport halibutRedisTransport)
        {
            var token = watchForCancellationTokenSource.Token;
            watchTask = Task.Run(async () => await WatchForExceptions(endpoint, requestActivityId, halibutRedisTransport, token));
        }

        async Task WatchForExceptions(Uri endpoint, Guid requestActivityId, HalibutRedisTransport halibutRedisTransport, CancellationToken token)
        {
            try
            {
                await using var _ = await halibutRedisTransport.SubscribeToRequestCancellation(endpoint, requestActivityId,
                    async _ =>
                    {
                        await requestCancelledCts.CancelAsync();
                        await watchForCancellationTokenSource.CancelAsync();
                    },
                    token);
                // Also poll to see if the request is cancelled since we can miss
                // the publication.
                while (!token.IsCancellationRequested)
                {
                    // TODO: What happens if this throws?
                    if (await halibutRedisTransport.IsRequestMarkedAsCancelled(endpoint, requestActivityId, token))
                    {
                        await requestCancelledCts.CancelAsync();
                        await watchForCancellationTokenSource.CancelAsync();
                    }
                    await Task.Delay(TimeSpan.FromMinutes(1), token);
                }
            }
            catch
            {
                // TODO log when we get an exception we don't expect.
            }
        }

        public async ValueTask DisposeAsync()
        {
            await Try.IgnoringError(async () => await watchForCancellationTokenSource.CancelAsync());
            Try.IgnoringError(() => watchForCancellationTokenSource.Dispose());
            await Try.IgnoringError(async () => await requestCancelledCts.CancelAsync());
            Try.IgnoringError(() => requestCancelledCts.Dispose());
        }
    }
}