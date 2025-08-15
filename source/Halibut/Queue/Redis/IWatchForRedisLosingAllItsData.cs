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

namespace Halibut.Queue.Redis
{
    public interface IWatchForRedisLosingAllItsData : IAsyncDisposable
    {
        /// <summary>
        /// Will cause the caller to wait until we are connected to redis and so can detect datalose.
        /// </summary>
        /// <param name="timeToWait">Time to wait for this to reach a state where it can detect datalose</param>
        /// <param name="cancellationToken"></param>
        /// <returns>A cancellation token which is triggered when data lose occurs.</returns>
        Task<CancellationToken> GetTokenForDataLoseDetection(TimeSpan timeToWait, CancellationToken cancellationToken);
    }
}