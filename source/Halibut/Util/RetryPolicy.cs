using System;
using System.Diagnostics;

namespace Halibut.Util
{
    class RetryPolicy
    {
        readonly Stopwatch stopwatch = new Stopwatch();

        internal RetryPolicy(double backoffMultiplier, TimeSpan minimumDelay, TimeSpan maximumDelay)
        {
            if (backoffMultiplier <= 0) throw new ArgumentOutOfRangeException(nameof(backoffMultiplier), "Must be greater than zero");
            if (backoffMultiplier > 1) throw new ArgumentOutOfRangeException(nameof(backoffMultiplier), "Must be less than or equal to one");

            BackoffMultiplier = backoffMultiplier;
            MinimumDelay = minimumDelay;
            MaximumDelay = maximumDelay;
        }

        internal TimeSpan MinimumDelay { get; }
        internal TimeSpan MaximumDelay { get; }
        internal double BackoffMultiplier { get; }

        public static RetryPolicy Create()
        {
            var backoffMultiplier = new Random().NextDouble();
            var minimumDelay = TimeSpan.FromSeconds(5);
            var maximumDelay = TimeSpan.FromMinutes(2);
            return new RetryPolicy(backoffMultiplier, minimumDelay, maximumDelay);
        }

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
            var delay = CalculateDelay(elapsed);
            return delay;
        }

        internal TimeSpan CalculateDelay(TimeSpan elapsed)
        {
            var multiple = 1d + BackoffMultiplier;
            var delay = TimeSpan.FromMilliseconds(elapsed.TotalMilliseconds * multiple);
            delay = delay > MinimumDelay
                ? delay
                : MinimumDelay;
            delay = delay < MaximumDelay
                ? delay
                : MaximumDelay;

            return delay;
        }
    }
}