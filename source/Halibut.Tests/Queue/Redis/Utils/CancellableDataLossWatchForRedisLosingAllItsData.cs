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
using Halibut.Queue.Redis;
using Halibut.Util;
using Try = Halibut.Tests.Support.Try;

namespace Halibut.Tests.Queue.Redis.Utils
{
    public class CancellableDataLossWatchForRedisLosingAllItsData : IWatchForRedisLosingAllItsData
    {
        CancelOnDisposeCancellationTokenSource cancellationTokenSource = new CancellationTokenSource().CancelOnDispose();

        public TaskCompletionSource<CancellationToken> TaskCompletionSource = new();
        public CancellableDataLossWatchForRedisLosingAllItsData()
        {
            TaskCompletionSource.SetResult(cancellationTokenSource.CancellationToken);
        }

        public async Task DataLossHasOccured()
        {
            await cancellationTokenSource.DisposeAsync();
            cancellationTokenSource = new CancellationTokenSource().CancelOnDispose();
            TaskCompletionSource = new TaskCompletionSource<CancellationToken>();
            TaskCompletionSource.SetResult(cancellationTokenSource.CancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            await Try.CatchingError(async () => await cancellationTokenSource.DisposeAsync());
        }

        public async Task<CancellationToken> GetTokenForDataLoseDetection(TimeSpan timeToWait, CancellationToken cancellationToken)
        {
#pragma warning disable VSTHRD003
            return await TaskCompletionSource.Task;
#pragma warning restore VSTHRD003
        }
    }
}