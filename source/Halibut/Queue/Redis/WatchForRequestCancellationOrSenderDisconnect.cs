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
using System.Security.Policy;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Diagnostics;
using Halibut.Util;

namespace Halibut.Queue.Redis
{
    
    public class WatchForRequestCancellationOrSenderDisconnect : IAsyncDisposable
    {
        readonly CancellationTokenSource RequestCancellationTokenSource;
        public CancellationToken RequestProcessingCancellationToken { get; }
        readonly WatchForRequestCancellation watchForRequestCancellation;

        readonly CancelOnDisposeCancellationTokenSource KeepWatchingCancellationTokenSource;
        
        DisposableCollection disposableCollection = new DisposableCollection();

        public WatchForRequestCancellationOrSenderDisconnect(
            Uri endpoint,
            Guid requestActivityId,
            HalibutRedisTransport halibutRedisTransport,
            TimeSpan nodeOfflineTimeoutBetweenHeartBeatsFromSender,
            ILog log)
        {
            try
            {
                watchForRequestCancellation = new WatchForRequestCancellation(endpoint, requestActivityId, halibutRedisTransport, log);
                disposableCollection.AddAsyncDisposable(watchForRequestCancellation);

                RequestCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(watchForRequestCancellation.RequestCancelledCancellationToken);
                disposableCollection.Add(RequestCancellationTokenSource);
                RequestProcessingCancellationToken = RequestCancellationTokenSource.Token;

                KeepWatchingCancellationTokenSource = new CancellationTokenSource().CancelOnDispose();
                disposableCollection.AddAsyncDisposable(KeepWatchingCancellationTokenSource);

                Task.Run(() => WatchThatNodeWhichSentTheRequestIsStillAlive(endpoint, requestActivityId, halibutRedisTransport, nodeOfflineTimeoutBetweenHeartBeatsFromSender, log));
            }
            catch (Exception)
            {
                Try.IgnoringError(async () => await disposableCollection.DisposeAsync()).GetAwaiter().GetResult();
                throw;
            }

        }

        async Task WatchThatNodeWhichSentTheRequestIsStillAlive(Uri endpoint, Guid requestActivityId, HalibutRedisTransport halibutRedisTransport, TimeSpan nodeOfflineTimeoutBetweenHeartBeatsFromSender, ILog log)
        {
            var watchCancellationToken = KeepWatchingCancellationTokenSource.CancellationToken;
            try
            {
                var res = await NodeHeartBeatSender
                    .WatchThatNodeWhichSentTheRequestIsStillAlive(endpoint, requestActivityId, halibutRedisTransport, log, nodeOfflineTimeoutBetweenHeartBeatsFromSender, watchCancellationToken);
                if (res == NodeHeartBeatSender.NodeProcessingRequestWatcherResult.NodeMayHaveDisconnected)
                {
                    await RequestCancellationTokenSource.CancelAsync();
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