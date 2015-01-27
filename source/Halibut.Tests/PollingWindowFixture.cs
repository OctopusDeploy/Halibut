using System;
using Halibut.Transport;
using NUnit.Framework;

namespace Halibut.Tests
{
    [TestFixture]
    public class PollingWindowFixture
    {
        PollingWindow window;

        [SetUp]
        public void SetUp()
        {
            window = new PollingWindow();
        }

        [Test]
        public void WhenIncrementedShouldSteadilyDecreasePollingFrequency()
        {
            Assert.That(window.Increment(), Is.EqualTo(TimeSpan.Zero));
            Assert.That(window.Increment(), Is.EqualTo(TimeSpan.Zero));
            Assert.That(window.Increment(), Is.EqualTo(TimeSpan.Zero));
            Assert.That(window.Increment(), Is.EqualTo(TimeSpan.Zero));
            Assert.That(window.Increment(), Is.EqualTo(TimeSpan.Zero));
            Assert.That(window.Increment(), Is.EqualTo(TimeSpan.FromMilliseconds(100)));
            Assert.That(window.Increment(), Is.EqualTo(TimeSpan.FromMilliseconds(100)));
            Assert.That(window.Increment(), Is.EqualTo(TimeSpan.FromMilliseconds(100)));
            Assert.That(window.Increment(), Is.EqualTo(TimeSpan.FromMilliseconds(100)));
            Assert.That(window.Increment(), Is.EqualTo(TimeSpan.FromMilliseconds(100)));
            Assert.That(window.Increment(), Is.EqualTo(TimeSpan.FromMilliseconds(1000)));
            Assert.That(window.Increment(), Is.EqualTo(TimeSpan.FromMilliseconds(1000)));
            Assert.That(window.Increment(), Is.EqualTo(TimeSpan.FromMilliseconds(1000)));
            Assert.That(window.Increment(), Is.EqualTo(TimeSpan.FromMilliseconds(1000)));
            Assert.That(window.Increment(), Is.EqualTo(TimeSpan.FromMilliseconds(1000)));
            Assert.That(window.Increment(), Is.EqualTo(TimeSpan.FromMilliseconds(10000)));
            Assert.That(window.Increment(), Is.EqualTo(TimeSpan.FromMilliseconds(10000)));
            Assert.That(window.Increment(), Is.EqualTo(TimeSpan.FromMilliseconds(10000)));
            Assert.That(window.Increment(), Is.EqualTo(TimeSpan.FromMilliseconds(10000)));
            Assert.That(window.Increment(), Is.EqualTo(TimeSpan.FromMilliseconds(10000)));
            Assert.That(window.Increment(), Is.EqualTo(TimeSpan.FromMilliseconds(30000)));
            Assert.That(window.Increment(), Is.EqualTo(TimeSpan.FromMilliseconds(30000)));
            Assert.That(window.Increment(), Is.EqualTo(TimeSpan.FromMilliseconds(30000)));
            Assert.That(window.Increment(), Is.EqualTo(TimeSpan.FromMilliseconds(30000)));
        }

        [Test]
        public void WhenResetShouldStartFromZero()
        {
            Assert.That(window.Increment(), Is.EqualTo(TimeSpan.Zero));
            Assert.That(window.Increment(), Is.EqualTo(TimeSpan.Zero));
            Assert.That(window.Increment(), Is.EqualTo(TimeSpan.Zero));
            Assert.That(window.Increment(), Is.EqualTo(TimeSpan.Zero));
            Assert.That(window.Increment(), Is.EqualTo(TimeSpan.Zero));
            Assert.That(window.Increment(), Is.EqualTo(TimeSpan.FromMilliseconds(100)));
            Assert.That(window.Increment(), Is.EqualTo(TimeSpan.FromMilliseconds(100)));
            Assert.That(window.Increment(), Is.EqualTo(TimeSpan.FromMilliseconds(100)));
            Assert.That(window.Increment(), Is.EqualTo(TimeSpan.FromMilliseconds(100)));
            Assert.That(window.Increment(), Is.EqualTo(TimeSpan.FromMilliseconds(100)));
            Assert.That(window.Increment(), Is.EqualTo(TimeSpan.FromMilliseconds(1000)));
            Assert.That(window.Increment(), Is.EqualTo(TimeSpan.FromMilliseconds(1000)));
            window.Reset();
            Assert.That(window.Increment(), Is.EqualTo(TimeSpan.Zero));
            Assert.That(window.Increment(), Is.EqualTo(TimeSpan.Zero));
            Assert.That(window.Increment(), Is.EqualTo(TimeSpan.Zero));
            Assert.That(window.Increment(), Is.EqualTo(TimeSpan.Zero));
            Assert.That(window.Increment(), Is.EqualTo(TimeSpan.Zero));
            Assert.That(window.Increment(), Is.EqualTo(TimeSpan.FromMilliseconds(100)));
            Assert.That(window.Increment(), Is.EqualTo(TimeSpan.FromMilliseconds(100)));
        }
    }
}