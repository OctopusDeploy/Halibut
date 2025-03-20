using System;
using FluentAssertions;
using NUnit.Framework;

namespace Halibut.Tests
{
    public class DependenciesTest
    {
        [Test]
        public void FluentAssertionsIsVersion7()
        {
            typeof(AssertionOptions).Assembly.GetName().Version!.Major
                .Should()
                .Be(
                    7,
                    "We want to keep using the FOSS version of FluentAssertions, which changed in v8."
                );
        }
    }
}