using System;
using System.Diagnostics;

namespace Halibut.Util
{
    class PhasedBackoffRetryTracker
    {
        readonly Stopwatch stopwatch = new Stopwatch();
        readonly Random random = new Random();

        public void Try()
        {
            stopwatch.Start();
        }

        public void Success()
        {
            stopwatch.Reset();
        }

        public TimeSpan GetSleepPeriod()
        {
            var elapsed = stopwatch.Elapsed;
            
            // Using a random interval prevents all the servers connecting back at the same time when the client comes back online
            if(elapsed < TimeSpan.FromMinutes(5))
                return TimeSpan.FromMilliseconds(random.Next(5_000, 10_000));
            
            if(elapsed < TimeSpan.FromHours(1))
                return TimeSpan.FromMilliseconds(random.Next(15_000, 30_000));
            
            return TimeSpan.FromMilliseconds(random.Next(60_000, 120_000));
        }
    }
}