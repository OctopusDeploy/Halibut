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

namespace Halibut.Queue.Redis
{
    public class WatchForRedisLosingAllItsData : IAsyncDisposable
    {
        RedisFacade redisFacade;
        readonly ILog log;
        
        internal TimeSpan SetupDelay { get;}
        internal TimeSpan WatchInterval { get; }
        internal TimeSpan KeyTTL { get;  }
        CancelOnDisposeCancellationTokenSource cancelOnDisposeCancellationTokenSource = new CancellationTokenSource().CancelOnDispose();

        public WatchForRedisLosingAllItsData(RedisFacade redisFacade, ILog log, TimeSpan? setupDelay = null, TimeSpan? watchInterval = null, TimeSpan? keyTTL = null)
        {
            this.redisFacade = redisFacade;
            this.log = log;
            this.SetupDelay = setupDelay ?? TimeSpan.FromSeconds(1);
            this.WatchInterval = watchInterval ?? TimeSpan.FromSeconds(60);
            this.KeyTTL = keyTTL ?? TimeSpan.FromMinutes(60);
            var _ = Task.Run(async () => await KeepWatchingForDataLose(cancelOnDisposeCancellationTokenSource.CancellationToken));
        }

        private TaskCompletionSource<CancellationToken> taskCompletionSource = new TaskCompletionSource<CancellationToken>();
        
        /// <summary>
        /// Will cause the caller to wait until we are connected to redis and so can detect datalose.
        /// </summary>
        /// <param name="timeToWait">Time to wait for this to reach a state where it can detect datalose</param>
        /// <param name="cancellationToken"></param>
        /// <returns>A cancellation token which is triggered when data lose occurs.</returns>
        public async Task<CancellationToken> GetTokenForDataLoseDetection(TimeSpan timeToWait, CancellationToken cancellationToken)
        {
            if (taskCompletionSource.Task.IsCompleted)
            {
                return await taskCompletionSource.Task;
            }
            
            await using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken).CancelOnDispose();
            cts.CancellationTokenSource.CancelAfter(timeToWait);
            return await taskCompletionSource.Task.WaitAsync(cts.CancellationToken);
        }

        private async Task KeepWatchingForDataLose(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Try.IgnoringError(async () => await WatchForDataLose(cancellationToken));
            }
        }

        async Task WatchForDataLose(CancellationToken cancellationToken)
        {
            string guid = Guid.NewGuid().ToString();
            var key = "WatchForDataLose::" + guid;
            var hasSetKey = false;
            
            log.Write(EventType.Diagnostic, "Starting Redis data loss monitoring with key {0}", key);
            
            await using var cts = new CancellationTokenSource().CancelOnDispose();
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (!hasSetKey)
                    {
                        log.Write(EventType.Diagnostic, "Setting initial data loss monitoring key {0} with TTL {1} minutes", key, KeyTTL.TotalMinutes);
                        await redisFacade.SetString(key, guid.ToString(), KeyTTL, cancellationToken);
                        taskCompletionSource.TrySetResult(cts.CancellationToken);
                        hasSetKey = true;
                        log.Write(EventType.Diagnostic, "Successfully set initial data loss monitoring key {0}, monitoring is now active", key);
                    }
                    else
                    {
                        var data = await redisFacade.GetString(key, cancellationToken);
                        if (data != guid.ToString())
                        {
                            log.Write(EventType.Error, "Redis data loss detected! Expected value {0} for key {1}, but got {2}. This indicates Redis has lost data.", guid.ToString(), key, data ?? "null");
                            // Anyone new will be given a new thing to wait on.
                            taskCompletionSource = new TaskCompletionSource<CancellationToken>();
                            await Try.IgnoringError(async () => await cts.CancellationTokenSource.CancelAsync());
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    log.Write(EventType.Diagnostic, "Error occurred during Redis data loss monitoring for key {0}: {1}. Will retry after delay.", key, ex.Message);
                }

                await Try.IgnoringError(async () =>
                {
                    if (!hasSetKey) await Task.Delay(SetupDelay, cancellationToken);
                    else await Task.Delay(WatchInterval, cancellationToken);
                });

            }

            log.Write(EventType.Diagnostic, "Redis data loss monitoring stopped for key {0}, cleaning up", key);
            await Try.IgnoringError(async () => await redisFacade.DeleteString(key));
        }

        public async ValueTask DisposeAsync()
        {
            log.Write(EventType.Diagnostic, "Disposing WatchForRedisLosingAllItsData");
            await cancelOnDisposeCancellationTokenSource.DisposeAsync();
        }
    }
}