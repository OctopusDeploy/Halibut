using System;
using FluentAssertions;
using Halibut.Tests.Support.TestAttributes;
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

        [Test]
        public void HowManyParallelTests()
        {
            int count = CustomLevelOfParallelismAttribute.NumberOfCpusToUse();
            
            Logger.Error("The test count is: " + count);
            count.Should().Be(0);
        }
    }
}