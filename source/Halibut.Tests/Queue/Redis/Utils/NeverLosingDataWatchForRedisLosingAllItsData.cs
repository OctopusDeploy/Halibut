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
#if NET8_0_OR_GREATER
using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Queue.Redis;

namespace Halibut.Tests.Queue.Redis.Utils
{
    /// <summary>
    /// Test implementation of IWatchForRedisLosingAllItsData that returns CancellationToken.None
    /// to indicate no data loss detection is active during testing.
    /// </summary>
    public class NeverLosingDataWatchForRedisLosingAllItsData : IWatchForRedisLosingAllItsData
    {
        public Task<CancellationToken> GetTokenForDataLoseDetection(TimeSpan timeToWait, CancellationToken cancellationToken)
        {
            return Task.FromResult(CancellationToken.None);
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
#endif