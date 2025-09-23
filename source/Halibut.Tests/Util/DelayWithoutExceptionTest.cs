using System;
using System.Threading;
using System.Threading.Tasks;
using Halibut.Util;
using NUnit.Framework;

namespace Halibut.Tests.Util
{
    public class DelayWithoutExceptionTest : BaseTest
    {
        [Test]
        public async Task DelayWithoutException_ShouldNotThrow()
        {
            using var cts = new CancellationTokenSource();
            cts.CancelAfter(10);
            await DelayWithoutException.Delay(TimeSpan.FromDays(1), cts.Token);
        }
    }
}