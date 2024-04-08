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
using Halibut.Util;

namespace Halibut.Tests.Support
{
    public class CountingRetryPolicy : RetryPolicy
    {
        public int TryCount { get; private set; }
        public int SuccessCount { get; private set; }
        public int GetSleepPeriodCount { get; private set; }

        public CountingRetryPolicy()
            : base(1, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10))
        {
        }

        public override void Try()
        {
            TryCount++;
            base.Try();
        }

        public override void Success()
        {
            SuccessCount++;
            base.Success();
        }

        public override TimeSpan GetSleepPeriod()
        {
            GetSleepPeriodCount++;
            return base.GetSleepPeriod();
        }
    }
}