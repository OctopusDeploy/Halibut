using System;
using FluentAssertions;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class HowManyCpus : BaseTest
    {
        [Test]
        public void HowMany()
        {
            var count = Environment.ProcessorCount;
            Logger.Error("The count is: " + count);
            count.Should().Be(0);
        }
    }
}