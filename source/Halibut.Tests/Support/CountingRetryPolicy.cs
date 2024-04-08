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