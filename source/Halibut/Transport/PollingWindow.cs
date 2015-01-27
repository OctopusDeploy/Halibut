using System;
using System.Linq;

namespace Halibut.Transport
{
    /// <summary>
    /// Keeps track of how frequently a remote machine should be polled for requests, starting immediately, then falling back to 100ms, then 1 second, then 10 seconds, then finally every 30 seconds.
    /// </summary>
    class PollingWindow
    {
        static readonly TimeSpan ThirtySeconds = TimeSpan.FromSeconds(30);
        static readonly TimeSpan[] Intervals;
        int currentIntervalIndex = -1;

        static PollingWindow()
        {
            Intervals = new TimeSpan[0]
                .Concat(Enumerable.Repeat(TimeSpan.Zero, 5))
                .Concat(Enumerable.Repeat(TimeSpan.FromMilliseconds(100), 5))
                .Concat(Enumerable.Repeat(TimeSpan.FromMilliseconds(1000), 5))
                .Concat(Enumerable.Repeat(TimeSpan.FromMilliseconds(10000), 5))
                .ToArray();
        }

        public TimeSpan Increment()
        {
            if (currentIntervalIndex + 1 >= Intervals.Length)
            {
                return ThirtySeconds;
            }

            currentIntervalIndex++;
            return Intervals[currentIntervalIndex];
        }

        public void Reset()
        {
            currentIntervalIndex = -1;
        }
    }
}