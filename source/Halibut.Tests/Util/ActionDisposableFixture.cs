using System.Collections.Generic;
using FluentAssertions;
using Halibut.Util;
using NUnit.Framework;

namespace Halibut.Tests.Util
{
    public class ActionDisposableFixture
    {
        [Test]
        public void DisposeIsCalledOnce()
        {
            var list = new List<string>();
            using (var actionDisposable = new ActionDisposable(() => list.Add("just dispose once")))
            {
                actionDisposable.Dispose();
            }

            list.Count.Should().Be(1);
        }
    }
}