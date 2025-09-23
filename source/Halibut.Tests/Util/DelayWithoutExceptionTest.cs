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
using Halibut.Util;
using NUnit.Framework;

namespace Halibut.Tests.Util
{
    public class DelayWithoutExceptionTest : BaseTest
    {
        [Test]
        public async Task DelayWithoutException_ShouldNotThrow()
        {
            using var cts = new CancellationTokenSource();
            cts.CancelAfter(10);
            await DelayWithoutException.Delay(TimeSpan.FromDays(1), cts.Token);
        }
    }
}