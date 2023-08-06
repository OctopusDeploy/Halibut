using System;
using Halibut.Util;
using NUnit.Framework;

namespace Halibut.Tests.Util
{
    public class RetryPolicyFixture
    {
        [Test]
        [TestCase(1, 1, 60, 0, ExpectedResult = 1)]
        [TestCase(1, 1, 60, 1, ExpectedResult = 2)]
        [TestCase(1, 1, 60, 2, ExpectedResult = 4)]
        [TestCase(1, 1, 60, 30, ExpectedResult = 60)]
        [TestCase(1, 1, 60, 60, ExpectedResult = 60)]
        [TestCase(0.5, 1, 120, 0, ExpectedResult = 1)]
        [TestCase(0.5, 1, 120, 2, ExpectedResult = 3)]
        [TestCase(0.5, 1, 120, 4, ExpectedResult = 6)]
        [TestCase(0.5, 1, 120, 30, ExpectedResult = 45)]
        [TestCase(0.5, 1, 120, 60, ExpectedResult = 90)]
        [TestCase(0.5, 1, 120, 120, ExpectedResult = 120)]
        [TestCase(1, 5, 120, 0, ExpectedResult = 5)]
        [TestCase(1, 5, 120, 1, ExpectedResult = 5)]
        [TestCase(1, 5, 120, 2, ExpectedResult = 5)]
        [TestCase(1, 5, 120, 3, ExpectedResult = 6)]
        [TestCase(1, 5, 120, 60, ExpectedResult = 120)]
        [TestCase(1, 5, 120, 120, ExpectedResult = 120)]
        public int RetryTimesShouldBeCorrect(double multiplier, int minimumSeconds, int maximumSeconds, int elapsedSeconds)
        {
            var policy = new RetryPolicy(multiplier, TimeSpan.FromSeconds(minimumSeconds), TimeSpan.FromSeconds(maximumSeconds));

            var elapsed = TimeSpan.FromSeconds(elapsedSeconds);
            var delay = policy.CalculateDelay(elapsed);

            return (int)delay.TotalSeconds;
        }

        [Test]
        [TestCase(0)]
        public void InvalidMultipliersShouldThrow(double multiplier)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RetryPolicy(multiplier, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)));
        }
    }
}