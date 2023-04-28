using System;
using System.Reflection;
using FluentAssertions;
using Halibut.Diagnostics;
using NUnit.Framework;

namespace Halibut.Tests.Diagnostics
{
    public class UnpackFromContainersFixture
    {
        [Test]
        public void JustAnException()
        {
            var e = new Exception();

            e.UnpackFromContainers().Should().Be(e);
        }


        [Test]
        public void HasInner()
        {
            var inner = new Exception("inner");
            var e = new Exception("outer", inner);

            e.UnpackFromContainers().Should().Be(e);
        }


        [Test]
        public void IsInvocationException()
        {
            var inner = new Exception("inner");
            var e = new TargetInvocationException("invocation", inner);

            e.UnpackFromContainers().Should().Be(inner);
        }

        [Test]
        public void IsAggregateException()
        {
            var inner = new Exception("inner");
            var inner2 = new Exception("inner2");
            var e = new AggregateException("invocation", inner, inner2);

            e.UnpackFromContainers().Should().Be(e);
        }
    }
}