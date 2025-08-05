using System;
using Halibut.Util;
using NUnit.Framework;

namespace Halibut.Tests.Util
{
    public class LinearBackoffStrategyFixture
    {
        [Test]
        [TestCase(1, 1, 30, 1, ExpectedResult = 1)]
        [TestCase(1, 1, 30, 2, ExpectedResult = 2)]
        [TestCase(1, 1, 30, 3, ExpectedResult = 3)]
        [TestCase(1, 1, 30, 10, ExpectedResult = 10)]
        [TestCase(1, 1, 30, 30, ExpectedResult = 30)]
        [TestCase(1, 1, 30, 31, ExpectedResult = 30)] // Should cap at maximum
        [TestCase(1, 1, 30, 50, ExpectedResult = 30)] // Should cap at maximum
        [TestCase(2, 3, 20, 1, ExpectedResult = 2)]   // initialDelay=2, increment=3
        [TestCase(2, 3, 20, 2, ExpectedResult = 5)]   // 2 + (2-1)*3 = 5
        [TestCase(2, 3, 20, 3, ExpectedResult = 8)]   // 2 + (3-1)*3 = 8
        [TestCase(2, 3, 20, 7, ExpectedResult = 20)]  // 2 + (7-1)*3 = 20 (at max)
        [TestCase(2, 3, 20, 8, ExpectedResult = 20)]  // Should cap at maximum
        [TestCase(0, 2, 10, 1, ExpectedResult = 0)]   // Zero initial delay
        [TestCase(0, 2, 10, 2, ExpectedResult = 2)]   // 0 + (2-1)*2 = 2
        [TestCase(0, 2, 10, 6, ExpectedResult = 10)]  // 0 + (6-1)*2 = 10 (at max)
        public int CalculateDelayForAttemptShouldBeCorrect(int initialDelaySeconds, int incrementSeconds, int maxDelaySeconds, int attemptNumber)
        {
            var strategy = new LinearBackoffStrategy(
                TimeSpan.FromSeconds(initialDelaySeconds),
                TimeSpan.FromSeconds(incrementSeconds),
                TimeSpan.FromSeconds(maxDelaySeconds)
            );

            var delay = strategy.CalculateDelayForAttempt(attemptNumber);

            return (int)delay.TotalSeconds;
        }

        [Test]
        public void GetSleepPeriodShouldReturnZeroWhenNoAttemptsMade()
        {
            var strategy = LinearBackoffStrategy.Create();
            
            var delay = strategy.GetSleepPeriod();
            
            Assert.That(delay, Is.EqualTo(TimeSpan.Zero));
        }

        [Test]
        public void GetSleepPeriodShouldIncreaseLinearlyWithAttempts()
        {
            var strategy = new LinearBackoffStrategy(
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(3),
                TimeSpan.FromSeconds(20)
            );

            // First attempt
            strategy.Try();
            Assert.That(strategy.GetSleepPeriod(), Is.EqualTo(TimeSpan.FromSeconds(2)));
            Assert.That(strategy.AttemptCount, Is.EqualTo(1));

            // Second attempt
            strategy.Try();
            Assert.That(strategy.GetSleepPeriod(), Is.EqualTo(TimeSpan.FromSeconds(5))); // 2 + (2-1)*3 = 5
            Assert.That(strategy.AttemptCount, Is.EqualTo(2));

            // Third attempt
            strategy.Try();
            Assert.That(strategy.GetSleepPeriod(), Is.EqualTo(TimeSpan.FromSeconds(8))); // 2 + (3-1)*3 = 8
            Assert.That(strategy.AttemptCount, Is.EqualTo(3));
        }

        [Test]
        public void GetSleepPeriodShouldCapAtMaximumDelay()
        {
            var strategy = new LinearBackoffStrategy(
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(20)
            );

            strategy.Try(); // Attempt 1: 5 seconds
            Assert.That(strategy.GetSleepPeriod(), Is.EqualTo(TimeSpan.FromSeconds(5)));

            strategy.Try(); // Attempt 2: 15 seconds
            Assert.That(strategy.GetSleepPeriod(), Is.EqualTo(TimeSpan.FromSeconds(15)));

            strategy.Try(); // Attempt 3: would be 25, but capped at 20
            Assert.That(strategy.GetSleepPeriod(), Is.EqualTo(TimeSpan.FromSeconds(20)));

            strategy.Try(); // Attempt 4: still capped at 20
            Assert.That(strategy.GetSleepPeriod(), Is.EqualTo(TimeSpan.FromSeconds(20)));
        }

        [Test]
        public void SuccessShouldResetAttemptCount()
        {
            var strategy = LinearBackoffStrategy.Create();

            strategy.Try();
            strategy.Try();
            strategy.Try();
            Assert.That(strategy.AttemptCount, Is.EqualTo(3));
            Assert.That(strategy.GetSleepPeriod(), Is.EqualTo(TimeSpan.FromSeconds(3)));

            strategy.Success();
            Assert.That(strategy.AttemptCount, Is.EqualTo(0));
            Assert.That(strategy.GetSleepPeriod(), Is.EqualTo(TimeSpan.Zero));

            // After reset, should start from the beginning again
            strategy.Try();
            Assert.That(strategy.AttemptCount, Is.EqualTo(1));
            Assert.That(strategy.GetSleepPeriod(), Is.EqualTo(TimeSpan.FromSeconds(1)));
        }

        [Test]
        public void CreateShouldReturnStrategyWithDefaultValues()
        {
            var strategy = LinearBackoffStrategy.Create();

            Assert.That(strategy.InitialDelay, Is.EqualTo(TimeSpan.FromSeconds(1)));
            Assert.That(strategy.Increment, Is.EqualTo(TimeSpan.FromSeconds(1)));
            Assert.That(strategy.MaximumDelay, Is.EqualTo(TimeSpan.FromSeconds(30)));
            Assert.That(strategy.AttemptCount, Is.EqualTo(0));
        }

        [Test]
        [TestCase(-1)] // Negative initial delay
        public void InvalidInitialDelayShouldThrow(int initialDelaySeconds)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => 
                new LinearBackoffStrategy(
                    TimeSpan.FromSeconds(initialDelaySeconds),
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromSeconds(10)
                )
            );
        }

        [Test]
        [TestCase(0)]  // Zero increment
        [TestCase(-1)] // Negative increment
        public void InvalidIncrementShouldThrow(int incrementSeconds)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new LinearBackoffStrategy(
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromSeconds(incrementSeconds),
                    TimeSpan.FromSeconds(10)
                )
            );
        }

        [Test]
        public void MaximumDelayLessThanInitialDelayShouldThrow()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new LinearBackoffStrategy(
                    TimeSpan.FromSeconds(10), // Initial delay
                    TimeSpan.FromSeconds(1),  // Increment
                    TimeSpan.FromSeconds(5)   // Maximum delay (less than initial)
                )
            );
        }

        [Test]
        [TestCase(0, ExpectedResult = 0)]
        [TestCase(-1, ExpectedResult = 0)]
        [TestCase(-10, ExpectedResult = 0)]
        public int CalculateDelayForAttemptShouldReturnZeroForInvalidAttemptNumbers(int attemptNumber)
        {
            var strategy = LinearBackoffStrategy.Create();
            var delay = strategy.CalculateDelayForAttempt(attemptNumber);
            return (int)delay.TotalSeconds;
        }

        [Test]
        public void PropertiesShouldReturnConstructorValues()
        {
            var initialDelay = TimeSpan.FromSeconds(3);
            var increment = TimeSpan.FromSeconds(2);
            var maximumDelay = TimeSpan.FromSeconds(15);

            var strategy = new LinearBackoffStrategy(initialDelay, increment, maximumDelay);

            Assert.That(strategy.InitialDelay, Is.EqualTo(initialDelay));
            Assert.That(strategy.Increment, Is.EqualTo(increment));
            Assert.That(strategy.MaximumDelay, Is.EqualTo(maximumDelay));
        }
    }
} 