using System;

namespace Halibut.Util
{
    /// <summary>
    /// A simple linear delay backoff strategy that increases the delay by a fixed increment 
    /// on each retry attempt (e.g., 1s, 2s, 3s, 4s, etc.).
    /// </summary>
    public class LinearBackoffStrategy
    {
        int attemptCount;

        public LinearBackoffStrategy(TimeSpan initialDelay, TimeSpan increment, TimeSpan maximumDelay)
        {
            if (initialDelay < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(initialDelay), "Initial delay must be non-negative");
            if (increment <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(increment), "Increment must be greater than zero");
            if (maximumDelay < initialDelay)
                throw new ArgumentOutOfRangeException(nameof(maximumDelay), "Maximum delay must be greater than or equal to initial delay");

            InitialDelay = initialDelay;
            Increment = increment;
            MaximumDelay = maximumDelay;
            attemptCount = 0;
        }

        public TimeSpan InitialDelay { get; }
        public TimeSpan Increment { get; }
        public TimeSpan MaximumDelay { get; }
        public int AttemptCount => attemptCount;

        /// <summary>
        /// Creates a LinearBackoffStrategy with sensible defaults: 
        /// Initial delay of 1 second, increment of 1 second, maximum delay of 30 seconds.
        /// </summary>
        public static LinearBackoffStrategy Create()
        {
            return new LinearBackoffStrategy(
                initialDelay: TimeSpan.FromSeconds(1),
                increment: TimeSpan.FromSeconds(1),
                maximumDelay: TimeSpan.FromSeconds(30)
            );
        }

        /// <summary>
        /// Records a retry attempt and increments the internal attempt counter.
        /// </summary>
        public virtual void Try()
        {
            attemptCount++;
        }

        /// <summary>
        /// Resets the backoff strategy after a successful operation.
        /// </summary>
        public virtual void Success()
        {
            attemptCount = 0;
        }

        /// <summary>
        /// Gets the delay period for the current attempt number.
        /// The delay increases linearly: initialDelay + (attemptCount - 1) * increment.
        /// </summary>
        public virtual TimeSpan GetSleepPeriod()
        {
            if (attemptCount <= 0)
            {
                return TimeSpan.Zero;
            }

            var delay = InitialDelay + TimeSpan.FromTicks((attemptCount - 1) * Increment.Ticks);

            // Cap at maximum delay
            return delay > MaximumDelay ? MaximumDelay : delay;
        }

        /// <summary>
        /// Calculates the delay for a specific attempt number without modifying internal state.
        /// </summary>
        public TimeSpan CalculateDelayForAttempt(int attemptNumber)
        {
            if (attemptNumber <= 0)
            {
                return TimeSpan.Zero;
            }

            var delay = InitialDelay + TimeSpan.FromTicks((attemptNumber - 1) * Increment.Ticks);
            
            // Cap at maximum delay
            return delay > MaximumDelay ? MaximumDelay : delay;
        }
    }
} 