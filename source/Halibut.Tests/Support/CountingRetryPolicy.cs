using System;
using System.Threading;
using Halibut.Util;

namespace Halibut.Tests.Support
{
    public class CountingRetryPolicy : RetryPolicy
    {
        int tryCount;
        int successCount;
        int getSleepPeriodCount;

        public int TryCount => tryCount;
        public int SuccessCount => successCount;
        public int GetSleepPeriodCount => getSleepPeriodCount;

        public CountingRetryPolicy()
            : this(1, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10))
        {
        }

        public CountingRetryPolicy(double backoffMultiplier, TimeSpan minimumDelay, TimeSpan maximumDelay)
            : base(backoffMultiplier, minimumDelay, maximumDelay)
        {
        }

        public override void Try()
        {
            Interlocked.Increment(ref tryCount);
            base.Try();
        }

        public override void Success()
        {
            Interlocked.Increment(ref successCount);
            base.Success();
        }

        public override TimeSpan GetSleepPeriod()
        {
            Interlocked.Increment(ref getSleepPeriodCount);
            return base.GetSleepPeriod();
        }
    }
}